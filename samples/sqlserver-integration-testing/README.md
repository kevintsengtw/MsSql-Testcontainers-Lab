# SQL Server 整合測試範例

本範例以 .NET 10、Dapper 與 Microsoft.Data.SqlClient 建立 API，測試使用 `WebApplicationFactory`、Testcontainers、xUnit Collection Fixture 與 Respawn，共用 SQL Server 容器，並在每個案例開始前重設資料。

## 文章與目錄

- [Testcontainers.MsSql 整合測試問題排解（一）：seed data 寫入逾時的判讀與 tmpfs 設定](articles/testcontainers-mssql-intermittent-timeout.md)
- [Testcontainers.MsSql 整合測試問題排解（二）：xUnit v3 升級與測試執行設定](articles/testcontainers-mssql-toolchain-traps.md)
- [根 README 的環境安裝與平台限制](../../README.md#環境需求與安裝準備)

```text
.
├─ MsSqlTestcontainersSample.slnx
├─ Directory.Packages.props
├─ global.json
├─ articles/
└─ Samples/
   ├─ src/Sample.WebApi/
   │  ├─ Controllers/
   │  ├─ Models/
   │  ├─ Repositories/
   │  └─ Infrastructure/
   ├─ tests/Sample.WebApi.IntegrationTests/
   │  ├─ Fixtures/
   │  ├─ Tests/
   │  ├─ InfrastructureTests/
   │  ├─ GlobalUsings.cs
   │  └─ xunit.runner.json
   └─ database/001_CreateTables.sql
```

既有 solution、專案、組件與命名空間均保留。內層 `Samples/` 維持 `ProjectReference` 與 SQL 輸出設定所需的相對位置。

## 執行前準備

先完成[環境需求與安裝準備](../../README.md#環境需求與安裝準備)：.NET 10 SDK、Docker Engine 就緒及 Linux 容器環境。SQL Server image 使用 x86-64；macOS Apple silicon 的轉譯環境不受 Microsoft SQL Server 官方支援，本次也未實測。
整合測試會自行建立容器、資料庫結構及 API 測試主機，不要求先手動啟動 SQL Server 或 API。

## 目前套件與 MTP 設定

版本集中於 [`Directory.Packages.props`](Directory.Packages.props)，啟用 CPM 與 transitive pinning；完整間接相依版本以該檔為準。

| 直接引用套件 | 版本 |
| --- | --- |
| Dapper | 2.1.89 |
| Microsoft.Data.SqlClient | 7.1.1 |
| System.Security.Cryptography.Pkcs | 10.0.13 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 |
| Testcontainers.MsSql | 4.16.0 |
| Respawn | 7.0.0 |
| xunit.v3.mtp-v2 | 4.0.2 |
| AwesomeAssertions | 9.6.0 |
| AutoFixture | 4.18.1 |

`xunit.v3.mtp-v2` 明確選擇 Microsoft Testing Platform v2。測試專案是 `OutputType=Exe` 的獨立執行檔，沒有引用 VSTest 的 `Microsoft.NET.Test.Sdk` 或 adapter。
[`global.json`](global.json) 指定 `Microsoft.Testing.Platform` runner；請先切換到本範例目錄，再執行 `dotnet test`。此檔只選擇 runner，沒有固定 SDK 版本。

[`xunit.runner.json`](Samples/tests/Sample.WebApi.IntegrationTests/xunit.runner.json) 設定 `parallelMode: "none"`，並保留舊鍵 `parallelizeTestCollections: false`。所有六個測試類別都繼承帶有 `[Collection]` 的 `IntegrationTestBase`，避免共享資料庫時平行互相干擾。
生命週期方法回傳 `ValueTask`；HTTP 與 JSON 讀取傳入 `TestContext.Current.CancellationToken`，這不表示所有資料庫 helper 都支援取消。

## 還原、建置與測試

以下從 repository 根目錄開始：

```powershell
cd samples/sqlserver-integration-testing
dotnet restore MsSqlTestcontainersSample.slnx
dotnet build MsSqlTestcontainersSample.slnx -c Release --no-restore
dotnet test MsSqlTestcontainersSample.slnx -c Release --no-build
```

首次執行會下載套件與容器映像檔。SQL Server image 保留 `mcr.microsoft.com/mssql/server:2022-latest`，未改用其他標籤；它是可變標籤。本次驗證使用 Windows x64 CLI，macOS、ARM64 與 IDE 測試視窗未實測。

## 容器、SQL 初始化與資料隔離

[`SqlServerContainerFixture.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/SqlServerContainerFixture.cs) 依序：

1. 建立 SQL Server 容器，將 `/var/opt/mssql/data` 掛成 tmpfs，套用記憶體預算，使用 Testcontainers.MsSql 內建等待策略。
2. 建立 `SampleDb`，設定 `RECOVERY SIMPLE` 與 `DELAYED_DURABILITY = FORCED`。
3. 依檔名順序執行輸出目錄 `database/` 的 SQL；測試專案的 `CopyToOutputDirectory="PreserveNewest"` 會複製 [`001_CreateTables.sql`](Samples/database/001_CreateTables.sql)。
4. 初始化 Respawn（`DbAdapter.SqlServer`、`WithReseed=true`），在 collection 結束時釋放容器。

[`IntegrationTestCollection.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/IntegrationTestCollection.cs) 讓 collection 共用一個容器；[`IntegrationTestBase.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/IntegrationTestBase.cs) 為每個測試建立 factory／client，開始前重設資料，結束後釋放。重設失敗時也會先清理 client 與 factory。
[`SampleWebApplicationFactory.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/SampleWebApplicationFactory.cs) 將容器的動態連線字串注入 API。資料建立統一使用 [`SeedDataHelper.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/SeedDataHelper.cs)，預設 `commandTimeout` 為 120 秒。

## tmpfs 與記憶體預算

[`ContainerMemoryBudget.cs`](Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/ContainerMemoryBudget.cs) 設定三個上限：

| 項目 | 設定 |
| --- | --- |
| 容器 cgroup 總量 | 4 GiB（`HostConfig.Memory`） |
| SQL Server 行程 | 2560 MB（`MSSQL_MEMORY_LIMIT_MB`） |
| `/var/opt/mssql/data` tmpfs | 512 MiB（`HostConfig.Mounts[].TmpfsOptions.SizeBytes`） |

tmpfs 頁面算入容器總量，以上預算約留 1 GiB 給其他行程與短暫尖峰，不能把 Docker 的可用記憶體全部分配給 SQL Server。這些是範例的額度分配，不代表所有環境的最低值。
`WithMemoryBudget()` 必須在 `WithTmpfsMount()` 之後呼叫；這個建置流程應修改 `HostConfig.Mounts`，不是 `HostConfig.Tmpfs`。

`/var/opt/mssql/data` 預設同時包含 `.mdf/.ndf` 與 `.ldf`。`/var/opt/mssql/log` 存放 error log 等診斷資料，不是交易記錄檔，因此目前沒有為它掛 tmpfs。
tmpfs 可能使用 swap；SIMPLE 不會自動縮小記錄檔；FORCED delayed durability 可能在故障時遺失已回應成功但尚未寫出的交易。這組設定用在可拋棄的測試資料庫，不能直接套用到正式環境。
系列（一）的效能表是 2026-08-24、舊版 Testcontainers 的歷史量測，不能當成現行範例在所有機器的速度或穩定性保證。

## API 與測試範圍

| API | 行為 |
| --- | --- |
| `GET /api/shippers`、`GET /api/shippers/{id}` | 集合與單筆查詢，找不到單筆回傳 404 |
| `POST /api/shippers` | 新增物流商，成功回傳 201 |
| `PUT /api/shippers/{id}`、`DELETE /api/shippers/{id}` | 修改／刪除成功回傳 204，不存在時 404 |
| `GET /api/orders/by-shipper/{shipperId}?page=1&pageSize=20` | 依物流商查詢訂單，以 OrderID 排序及分頁 |

`CompanyName` 必填、不可為空白、最多 40 個字元；`Phone` 最多 24 個字元。`page` 至少為 1，`pageSize` 為 1～200；位移使用 `long` 計算。DataAnnotations 與 `[ApiController]` 回傳 400 的 `ValidationProblemDetails`。
資料庫唯一限制判定重複公司名稱；[`SqlConstraintExceptionHandler.cs`](Samples/src/Sample.WebApi/Infrastructure/SqlConstraintExceptionHandler.cs) 將 SQL 2601／2627 與 547 轉為 409，不傳回 SQL 原始訊息。支援 JSON 的請求取得 `ProblemDetails`，不支援的 Accept 格式仍回傳 409，但沒有本文。

六個測試類別保留完整，預期共 42 個案例（含 Theory 資料列）：

| 類別 | 範圍 |
| --- | --- |
| [`ShippersApiTests`](Samples/tests/Sample.WebApi.IntegrationTests/Tests/ShippersApiTests.cs) | CRUD、400／409、Accept 協商、重複名稱與同時新增、失敗後資料狀態 |
| [`OrdersApiTests`](Samples/tests/Sample.WebApi.IntegrationTests/Tests/OrdersApiTests.cs) | 分頁參數、上限、物流商隔離與極大頁碼 |
| [`OrdersBulkSeedTests`](Samples/tests/Sample.WebApi.IntegrationTests/Tests/OrdersBulkSeedTests.cs) | 2000 筆 seed、第三頁的範圍與排序、末頁及超出範圍 |
| [`OrderRepositoryTransactionTests`](Samples/tests/Sample.WebApi.IntegrationTests/Tests/OrderRepositoryTransactionTests.cs) | 另一條連線讀取已提交內容；外鍵錯誤 547 與全部回復 |
| [`DatabaseResetTests`](Samples/tests/Sample.WebApi.IntegrationTests/InfrastructureTests/DatabaseResetTests.cs) | 同一案例先寫入再重設，清空兩表與重設兩個 IDENTITY |
| [`SqlServerStorageLayoutTests`](Samples/tests/Sample.WebApi.IntegrationTests/InfrastructureTests/SqlServerStorageLayoutTests.cs) | `.mdf/.ldf` 在 data 目錄，error log 在 log 目錄 |

後兩個類別直接驗證範例主題，因此保留在 `InfrastructureTests/`。

## 單獨執行 API

這是額外的操作方式，與自動建立容器的測試流程不同。先準備可用資料庫並執行 [`001_CreateTables.sql`](Samples/database/001_CreateTables.sql)，再提供 `ConnectionStrings:SampleDb`。預設 `appsettings.json` 的值為空。

下列 PowerShell 指令從本範例目錄執行，以遮罩輸入完整連線字串，僅傳給目前行程及其子行程；API 結束後移除變數。不要在 repository 或 shell 歷史中填入真實密碼。

```powershell
$env:ConnectionStrings__SampleDb = Read-Host '輸入已初始化資料庫的連線字串' -MaskInput
try {
    dotnet run --project Samples/src/Sample.WebApi/Sample.WebApi.csproj -c Release --no-build
}
finally {
    Remove-Item Env:ConnectionStrings__SampleDb
}
```

`-MaskInput` 需要 PowerShell 7.1 以上；macOS 可使用 PowerShell 7，或依自己的 shell 以行程環境變數提供同一設定鍵。本次未驗證這項手動連線操作；完整整合測試使用 TestServer 與自動容器，不需要這一步。

## 疑難排解

登入前交握逾時請先核對完整訊息、Docker 狀態與本機網路防護。作者曾在特定環境遇到 AdGuard 的 `Filter localhost` 影響，見系列（二）的[補充說明](articles/testcontainers-mssql-toolchain-traps.md#補充windows-上的登入前交握逾時)。這不是所有讀者的前提，也不要求設定 `host.override=localhost`。
測試正常結束應釋放本次的 SQL Server 與資源清理容器；若中斷後有殘留，先比對執行前後的容器 ID 與標籤，只處理已確認屬於本次測試的資源。

## 授權適用範圍

Copyright (c) 2026 Kevin Tseng。

本範例的自有程式、測試、專案設定與本 README 採 [MIT](../../LICENSE)；文章文字採 [CC BY 4.0](../../LICENSE-CC-BY-4.0)，文章中的自有程式碼、指令與設定範例（含行內與區塊）採 MIT。第三方內容保留原授權。
完整適用範圍與分享條件見[根 README](../../README.md#授權適用範圍)。
