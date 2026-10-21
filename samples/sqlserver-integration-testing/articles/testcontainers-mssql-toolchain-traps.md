# Testcontainers.MsSql 整合測試問題排解（二）：xUnit v3 升級與測試執行設定

範例從 `xunit` 2.9.3 遷移到 xUnit v3 後，除了修改生命週期方法，還需要調整測試專案
與 `dotnet test` 的執行設定。目前使用 `xunit.v3.mtp-v2 4.0.2`，明確選擇 Microsoft Testing Platform（MTP）v2，
這篇以 `.NET 10` 的現行範例說明。
[系列（一）](testcontainers-mssql-intermittent-timeout.md)則整理 seed data 寫入逾時的判讀與 tmpfs 設定。

## 一、測試專案變成了執行檔

先確認套件名稱：xUnit v3 有 `xunit.v3` 與明確選擇 MTP 的套件變體，框架世代與套件版號分開。
本範例使用 `xunit.v3.mtp-v2`，版號是 4.0.2，所以這仍是 xUnit v3。

**`IAsyncLifetime` 現在繼承 `IAsyncDisposable`**，兩個方法都改回傳 `ValueTask`：

```csharp
public async ValueTask InitializeAsync()
public async ValueTask DisposeAsync()
```

還有一個連帶的行為變化：同時實作 `IDisposable` 與 `IAsyncDisposable` 時，v3 只呼叫
`DisposeAsync`，不再兩個都叫。

也要留意初始化失敗：測試物件的 `InitializeAsync` 若拋出例外，xUnit 不會再替該物件呼叫清理方法。
範例的測試基底在建構式建立 client 與 factory，所以重設資料庫失敗時，
會先釋放這兩項資源再重新拋出例外。

**測試專案變成獨立執行檔**，csproj 要補一行：

```xml
<OutputType>Exe</OutputType>
```

`ITestOutputHelper` 也從 `Xunit.Abstractions` 移回 `Xunit`，刪掉那行 using 就好。

## 二、讓 dotnet test 使用 MTP

改完程式碼、建置成功，`dotnet test` 卻直接失敗：

```text
Testing with VSTest target is no longer supported by Microsoft.Testing.Platform
on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new
dotnet test experience.
```

這個專案採用 MTP。搭配 `.NET 10 SDK` 時，需要在 `global.json` 指定測試 runner，
讓 `dotnet test` 使用 MTP 的執行方式。

我先試了 MSBuild 屬性 `TestingPlatformDotnetTestSupport`，沒用；再試 `dotnet.config`，
也沒用。最後是去讀那個丟出錯誤的 targets 檔，看到裡面一個叫 `_SupportsGlobalJsonTestRunner`
的變數名，才知道開關在 `global.json`：

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

加上它，`dotnet test` 就正常了。這個檔案放在公開 repository 的 `samples/sqlserver-integration-testing/` 範例目錄，
執行時也先切到該目錄，讓 .NET CLI 找到 `global.json`。

範例目前只使用 MTP，已移除 `Microsoft.NET.Test.Sdk` 與 `xunit.runner.visualstudio`。
測試專案直接引用：

```xml
<PackageReference Include="xunit.v3.mtp-v2" />
```

版號 4.0.2 放在 `Directory.Packages.props` 集中管理。若其他專案仍需支援 VSTest 入口，
可保留 Test SDK 與 adapter；是否需要取決於實際使用的命令列與 IDE。
選擇方式見
[xUnit 官方 MTP 文件](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)。

## 三、平行執行與取消權杖

v3 4.0 起使用 `parallelMode` 控制平行執行，可選循序、collection 間平行或全部平行。
目前範例設為 `"none"`，以循序執行共享資料庫的測試。舊鍵 `parallelizeTestCollections` 仍可用，
會自動對映，也不會覆寫明確設定的新鍵。

測試內呼叫接受 `CancellationToken` 的方法時，xUnit analyzer 會以 `xUnit1051` 提醒傳入測試的取消權杖。
寫法如下：

```csharp
var actual = await this.Client.GetFromJsonAsync<List<Shipper>>(
    "/api/shippers", TestContext.Current.CancellationToken);
```

目前範例的 HTTP 呼叫與 JSON 回應讀取都傳入測試取消權杖。它讓這些呼叫能回應測試取消；
資料庫 helper 與 fixture 是否能取消，仍取決於它們有沒有往下傳遞取消權杖。
沒有 analyzer 警告，不等於整個測試流程都已具備取消處理。

## 補充：Windows 上的登入前交握逾時

本機另曾遇到 SQL Server 登入前交握逾時，後來確認與 AdGuard 的 `Filter localhost` 有關。
在 `Settings → App settings → Advanced settings` 關閉這個選項後，連線問題已排除。
若遇到同類現象，可以先檢查本機網路防護，
[AdGuard #6242](https://github.com/AdguardTeam/AdguardForWindows/issues/6242)有對應的案例記錄。

## 四、確認升級後的執行入口

生命週期方法、`OutputType`、`global.json` 與平行設定改完後，先建置再執行全部測試。
命令列與 IDE 可能使用不同的 runner；平常會用到的入口都應實際驗證。

目前範例有 42 個案例，包含 Theory 的資料列。API 測試除了成功回應，也檢查 400 的欄位錯誤與
409 的衝突回應；實際 HTTP 請求會經過 `SqlConstraintExceptionHandler`，驗證資料庫限制轉成
`ProblemDetails` 的處理流程。
衝突測試也涵蓋 `Accept`：JSON 請求取得 `ProblemDetails`；不支援的 `text/plain` 或
`application/xml` 則取得沒有本文的 409，避免錯誤處理本身再拋出例外。

完整的範例專案在 [GitHub](https://github.com/kevintsengtw/MsSql-Testcontainers-Lab/tree/main/samples/sqlserver-integration-testing)，
容器與記憶體設定請參考[系列（一）](testcontainers-mssql-intermittent-timeout.md)。

## 範例來源與授權

操作步驟與程式碼索引見[範例 README](../README.md)，兩篇文章共用同一份範例。
Copyright (c) 2026 Kevin Tseng。
文章文字採 [CC BY 4.0](../../../LICENSE-CC-BY-4.0)；其中的自有程式碼、指令與設定範例（含行內與區塊）採 [MIT](../../../LICENSE)。第三方內容保留原授權。
分享文章或改作時，請依 CC BY 4.0 保留作者署名、著作權與授權聲明、原文連結，並標示是否修改。完整範圍見[根 README](../../../README.md#授權適用範圍)。

本文的方法簽章、設定及 C# 呼叫是說明用片段，需放回對應專案，不是可單獨編譯的完整程式。
對應檔案：[`global.json`](../global.json)、
[`測試專案`](../Samples/tests/Sample.WebApi.IntegrationTests/Sample.WebApi.IntegrationTests.csproj)、
[`IntegrationTestBase.cs`](../Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/IntegrationTestBase.cs)、
[`ShippersApiTests.cs`](../Samples/tests/Sample.WebApi.IntegrationTests/Tests/ShippersApiTests.cs)。
