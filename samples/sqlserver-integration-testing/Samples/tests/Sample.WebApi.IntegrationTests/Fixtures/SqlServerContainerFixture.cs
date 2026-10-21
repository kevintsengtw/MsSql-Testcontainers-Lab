using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Respawn;
using Testcontainers.MsSql;

namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// Collection Fixture：整個測試 collection 共用同一個 SQL Server 容器。
/// 重點設定：
/// 1. SQL Server 預設的資料庫檔案目錄掛載 tmpfs（RAM 檔案系統）。
///    /var/opt/mssql/data 同時包含資料檔（.mdf/.ndf）與交易記錄檔（.ldf），
///    可降低 Docker Desktop / WSL2 儲存路徑造成的 I/O 變異。
///    /var/opt/mssql/log 放的是 error log、Extended Events 與 dump，不是交易記錄檔，
///    不在 WRITELOG 的寫入路徑上，因此不需要為了資料庫寫入效能掛成 tmpfs。
/// 2. 測試資料庫設定 RECOVERY SIMPLE + DELAYED_DURABILITY，測試資料不需要耐久性保證。
/// 3. 透過 Respawn 在測試之間重設資料，而非重建容器。
/// 4. 等待策略沿用內建預設值，見 <see cref="BuildContainer"/>。
/// </summary>
public class SqlServerContainerFixture : IAsyncLifetime
{
    private const string DatabaseName = "SampleDb";

    private const string ImageName = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = BuildContainer();

    private Respawner _respawner = null!;

    /// <summary>
    /// 建立容器：資料庫檔案目錄掛 tmpfs、套用兩層記憶體上限。
    /// 等待策略沿用 Testcontainers.MsSql 內建的預設值（容器 running + 容器內 sqlcmd 跑得動
    /// SELECT 1）。2026-08-24 的量測中，自訂等待策略增加約 0.57 秒；
    /// 兩組均未觀察到失敗，因此沒有足夠證據判定哪一組較穩定。
    /// </summary>
    private static MsSqlContainer BuildContainer()
    {
        // 現用版本的無參數 MsSqlBuilder() 已標記為過時，映像檔由建構式傳入。
        return new MsSqlBuilder(ImageName)
            // SQL Server on Linux 預設把使用者資料庫與系統資料庫的資料檔（.mdf/.ndf）
            // 和交易記錄檔（.ldf）都放在這個目錄。這才是與 WRITELOG 相關的掛載點。
            .WithTmpfsMount("/var/opt/mssql/data")
            // 記憶體預算：容器總量與 SQL Server 本體上限是兩層不同的東西，
            // 兩層都要設，理由見 ContainerMemoryBudget。必須排在 WithTmpfsMount 之後。
            .WithMemoryBudget()
            .Build();
    }

    /// <summary>
    /// 指向 SampleDb 的連線字串，供 WebApplicationFactory 與測試程式使用。
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// 容器啟動（含等待策略）所花的時間。
    /// </summary>
    public TimeSpan ContainerStartDuration { get; private set; }

    /// <summary>
    /// 從呼叫 StartAsync 到資料庫可用（含建庫、建表、Respawn 初始化）的總時間。
    /// </summary>
    public TimeSpan TotalReadyDuration { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        await this._container.StartAsync();
        this.ContainerStartDuration = stopwatch.Elapsed;

        // 模組預設連到 master，改指向我們自己的測試資料庫
        var builder = new SqlConnectionStringBuilder(this._container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
            ConnectTimeout = 30
        };
        this.ConnectionString = builder.ConnectionString;

        await this.CreateDatabaseAsync();
        await this.CreateSchemaAsync();

        // Respawn 7.0.0 移除了吃連線字串的多載，一律改吃已開啟的 DbConnection
        await using var respawnConnection = new SqlConnection(this.ConnectionString);
        await respawnConnection.OpenAsync();

        this._respawner = await Respawner.CreateAsync(
            respawnConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                WithReseed = true
            });

        this.TotalReadyDuration = stopwatch.Elapsed;
    }

    public async ValueTask DisposeAsync()
    {
        await this._container.DisposeAsync();
    }

    /// <summary>
    /// 於每個測試開始前呼叫，將資料庫重設為乾淨狀態（保留結構、清空資料、重設 IDENTITY）。
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await this._respawner.ResetAsync(connection);
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new SqlConnection(this._container.GetConnectionString());
        await connection.OpenAsync();

        // SIMPLE 讓不再需要的 log 空間在 checkpoint 後可重用，不會自動縮小檔案。
        // DELAYED_DURABILITY 讓 commit 不等待當次 log flush；只用於可拋棄的測試資料。
        await connection.ExecuteAsync(
            $"""
             CREATE DATABASE [{DatabaseName}];
             ALTER DATABASE [{DatabaseName}] SET RECOVERY SIMPLE;
             ALTER DATABASE [{DatabaseName}] SET DELAYED_DURABILITY = FORCED;
             """,
            commandTimeout: 120);
    }

    private async Task CreateSchemaAsync()
    {
        var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "database");
        var scripts = Directory.GetFiles(scriptDirectory, "*.sql").OrderBy(x => x);

        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();

        foreach (var script in scripts)
        {
            var sql = await File.ReadAllTextAsync(script);
            await connection.ExecuteAsync(sql, commandTimeout: 120);
        }
    }
}
