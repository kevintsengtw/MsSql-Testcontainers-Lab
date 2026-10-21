# MsSql-Testcontainers-Lab

這個 repository 收錄 `Testcontainers.MsSql` 整合測試的兩篇系列文章與範例程式，示範 SQL Server 的 tmpfs 與記憶體預算設定，以及 xUnit v3、Microsoft Testing Platform（MTP）和 Respawn 的使用方式。

## 文章與範例

| 內容 | 連結 |
| --- | --- |
| 系列（一）：seed data 寫入逾時的判讀與 tmpfs 設定 | [正式文章](samples/sqlserver-integration-testing/articles/testcontainers-mssql-intermittent-timeout.md) |
| 系列（二）：xUnit v3 升級與測試執行設定 | [正式文章](samples/sqlserver-integration-testing/articles/testcontainers-mssql-toolchain-traps.md) |
| 架構、套件版本、API 與測試範圍 | [範例 README](samples/sqlserver-integration-testing/README.md) |

```text
.
├─ .gitignore
├─ .gitattributes
├─ README.md
├─ LICENSE
├─ LICENSE-CC-BY-4.0
└─ samples/
   └─ sqlserver-integration-testing/
      ├─ README.md
      ├─ MsSqlTestcontainersSample.slnx
      ├─ Directory.Packages.props
      ├─ global.json
      ├─ articles/
      │  ├─ testcontainers-mssql-intermittent-timeout.md
      │  └─ testcontainers-mssql-toolchain-traps.md
      └─ Samples/
         ├─ src/Sample.WebApi/
         ├─ tests/Sample.WebApi.IntegrationTests/
         └─ database/001_CreateTables.sql
```

外層 `samples/` 組織不同主題，內層 `Samples/` 保留既有 solution、專案參照及 SQL 檔案的相對位置。

## 環境需求與安裝準備

### 1. 確認 SQL Server 映像檔的平台限制

範例使用 `mcr.microsoft.com/mssql/server:2022-latest`。本次讀取的映像檔是 `linux/amd64`，請準備可執行 Linux x86-64 容器的環境。
Microsoft 的[SQL Server 容器部署說明](https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver16)指出，官方支援範圍限於 Intel／AMD x86-64 CPU 的 Linux 主機，Rosetta 2、Prism、QEMU 等轉譯或模擬環境未經測試且不受支援。

| 開發環境 | 本範例的說明 |
| --- | --- |
| Windows x64 + Docker Desktop Linux 容器 | 本次驗證環境；開發測試結果不代表 Microsoft 的正式環境支援承諾 |
| macOS Intel + Docker Desktop | 可依下列步驟準備 x86-64 Linux 容器環境，本次未實測 |
| macOS Apple silicon／Windows ARM64 | SQL Server 映像不是原生 ARM64；即使透過轉譯啟動，也不在上述 SQL Server 官方支援範圍，本次未實測 |
| Linux x86-64 + Docker Engine | 符合映像檔架構方向，本次未實測 |

Docker Desktop 有 Apple silicon 安裝檔，不表示 SQL Server 同時支援 ARM64。[Docker 的 VMM 說明](https://docs.docker.com/desktop/features/vmm/)也列出 Docker VMM 尚不支援 Rosetta；不同虛擬化後端的轉譯能力不同。本文不提供 Apple silicon 已通過的保證或要求讀者改用其他 SQL Server image。

### 2. 安裝並啟動 Docker Desktop

**Windows x64**

1. 依[官方 Windows 安裝說明](https://docs.docker.com/desktop/setup/install/windows-install/)核對目前支援的 Windows 版本。使用 WSL 2 時須先在 BIOS／UEFI 啟用硬體虛擬化，啟用 Windows 的 WSL 2 功能並完成更新；步驟見[Microsoft WSL 安裝說明](https://learn.microsoft.com/en-us/windows/wsl/install)。
2. 安裝 x86-64 版 Docker Desktop，選擇 WSL 2 後端。依安裝程式要求重新啟動電腦，再啟動 Docker Desktop，完成首次設定並等待 Docker Engine 就緒。
3. 使用 Linux 容器。若目前的安裝可切換容器模式且正在使用 Windows 容器，請切換至 Linux 容器；本範例的 SQL Server image 不能以 Windows 容器模式執行。

**macOS**

1. 依[官方 Mac 安裝說明](https://docs.docker.com/desktop/setup/install/mac-install/)確認 macOS 版本，依 Intel 或 Apple silicon 選擇安裝檔。Apple silicon 仍有前述 SQL Server 限制。
2. 開啟 `Docker.dmg`，將 Docker 拖到「應用程式」，啟動 Docker Desktop，完成首次設定並等待 Engine 就緒。
3. 確認 Engine 可執行 Linux 容器。安裝成功只代表 Docker 可用，仍須分別核對 SQL Server 映像架構與實際執行結果。

範例在 [`ContainerMemoryBudget.cs`](samples/sqlserver-integration-testing/Samples/tests/Sample.WebApi.IntegrationTests/Fixtures/ContainerMemoryBudget.cs) 設定容器總量上限 4 GiB、SQL Server 行程上限 2560 MB、data tmpfs 上限 512 MiB。這些是上限，不是啟動時預先保留的全部記憶體；Docker／WSL 的可用額度仍須容納實際用量及其他容器，主機也要留空間給 SDK 與 IDE。若額度不足，先查 OOM 或容器退出狀態，不將這組預算當成所有工作負載的最低需求。

### 3. 安裝 .NET 10 SDK

從[.NET 10 官方下載頁](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)的 **SDK** 區域選擇作業系統與 CPU 架構：Windows x64、macOS Intel x64、macOS Apple silicon Arm64。後者僅指 SDK 的原生架構，不解除 SQL Server 的平台限制。
只裝 .NET Runtime 或 ASP.NET Core Runtime 無法完成這裡的還原、編譯與測試。安裝後重新開啟終端機及 IDE，讓 `PATH` 更新。

### 4. 選擇編輯器或 IDE

使用支援 .NET 10 的版本，以下擇一即可；本次驗證使用 CLI，未操作 IDE 測試視窗。

| 工具 | 開啟方式 |
| --- | --- |
| [VS Code](https://code.visualstudio.com/docs/languages/csharp) + C# Dev Kit | 開啟 `samples/sqlserver-integration-testing/` 資料夾，在整合式終端機執行下列指令 |
| [Rider](https://www.jetbrains.com/rider/download/) | 開啟 `samples/sqlserver-integration-testing/MsSqlTestcontainersSample.slnx` |
| Windows 的 [Visual Studio 2026](https://learn.microsoft.com/en-us/visualstudio/install/modify-visual-studio?view=visualstudio) | 透過 Visual Studio Installer 安裝「ASP.NET 與網頁程式開發」工作負載，再開啟同一份 solution |

### 5. 檢查環境與下載需求

在 PowerShell 或 macOS 終端機執行：

```powershell
dotnet --list-sdks
docker version
docker info --format '{{.OSType}}'
```

| 指令／狀況 | 預期結果與處理方式 |
| --- | --- |
| `dotnet --list-sdks` | 包含 `10.0.xxx`；找不到 `dotnet` 或只有 Runtime 時，安裝 SDK 並重開終端機 |
| `docker version` | 同時顯示 Client 與 Server；只有 Client 或無法連線時，啟動 Docker Desktop，等 Engine 就緒，並用 `docker context show` 核對目標 |
| `docker info --format '{{.OSType}}'` | 輸出 `linux`；若為 `windows`，切換至 Linux 容器 |
| `NETSDK1045` | 在範例目錄用 `dotnet --version` 核對 SDK，檢查上層 `global.json` 是否限制舊版 |
| Docker 顯示存取被拒 | 確認目前使用者可存取選定的 Docker Engine，依 Docker 的安裝與權限說明處理 |
| NuGet 還原或拉取 image 失敗 | 保留錯誤，檢查網路、套件來源、Docker 代理伺服器與登錄服務連線 |

首次執行需要下載 NuGet 套件、SQL Server 與 Testcontainers 的資源清理映像檔，請保留網路與磁碟空間。後續可能使用本機快取。`2022-latest` 是可變標籤，未來拉取的內容可能不同；本次映像 digest 留在公開目錄外的驗證紀錄。

## 還原、建置與整合測試

以下從本 repository 根目錄開始，先進入含有 `global.json` 的範例目錄，再使用同一個 Release 組態：

```powershell
cd samples/sqlserver-integration-testing
dotnet restore MsSqlTestcontainersSample.slnx
dotnet build MsSqlTestcontainersSample.slnx -c Release --no-restore
dotnet test MsSqlTestcontainersSample.slnx -c Release --no-build
```

以上 `cd` 與 `dotnet` 指令也可在 macOS shell 使用，但 macOS 本次未實測。不要只在 repository 根目錄傳入巢狀 solution 路徑，就假設 CLI 會讀取範例內的 `global.json`。
測試會自行建立 SQL Server 容器與 API 測試主機，無須先安裝另一套 SQL Server 或手動啟動 API。現行測試清單預期為六個類別、42 個案例（含 Theory 資料列），執行結果以當次輸出為準。

## 單獨執行 API

整合測試自動注入容器連線字串；單獨啟動 API 則需另行提供可連線且已執行 `Samples/database/001_CreateTables.sql` 的資料庫。`appsettings.json` 的 `ConnectionStrings:SampleDb` 預設為空，單憑 `dotnet run` 無法取得可用的資料庫環境。
行程層級的連線字串設定與啟動方式見[範例 README](samples/sqlserver-integration-testing/README.md#單獨執行-api)。請勿把真實密碼寫入 repository。

## 授權適用範圍

Copyright (c) 2026 Kevin Tseng。

| 內容 | 授權 |
| --- | --- |
| 自有範例程式、測試、專案設定及各層 README | [MIT](LICENSE) |
| `articles/` 文章文字 | [CC BY 4.0](LICENSE-CC-BY-4.0) |
| 文章中的自有程式碼、指令與設定範例（含行內與區塊） | [MIT](LICENSE) |

使用或散布上述 MIT 內容時，依 MIT 條款保留著作權及授權聲明。分享文章文字或改作時，依 CC BY 4.0 保留作者署名、著作權與授權聲明、原文連結，並標示是否修改。
第三方套件、映像檔與引用內容維持其原有授權，完整條文以本 repository 的授權檔為準。
