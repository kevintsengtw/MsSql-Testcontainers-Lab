using Microsoft.Data.SqlClient;

namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// 整合測試基底：
/// 1. 每個測試開始前以 Respawn 重設資料庫（IAsyncLifetime.InitializeAsync）
/// 2. 提供共用的 WebApplicationFactory、HttpClient 與資料庫連線
/// 3. 測試結束時 Dispose factory，避免 host / 連線洩漏累積
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly SqlServerContainerFixture _fixture;

    protected IntegrationTestBase(SqlServerContainerFixture fixture)
    {
        this._fixture = fixture;
        this.Factory = new SampleWebApplicationFactory(fixture.ConnectionString);
        this.Client = this.Factory.CreateClient();
    }

    protected SampleWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; }

    protected string ConnectionString => this._fixture.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await this._fixture.ResetDatabaseAsync();
        }
        catch
        {
            // xUnit 不會替初始化失敗的測試物件呼叫 DisposeAsync。
            // 此時 constructor 已建立 client 與 factory，必須在這裡釋放。
            await this.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        this.Client.Dispose();
        await this.Factory.DisposeAsync();
    }

    /// <summary>
    /// 建立指向測試資料庫的連線（呼叫端負責 Dispose）。
    /// </summary>
    protected SqlConnection CreateConnection()
    {
        return new SqlConnection(this.ConnectionString);
    }
}
