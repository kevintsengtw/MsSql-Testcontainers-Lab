using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// 將 WebApi 的連線字串替換為 Testcontainers 容器的連線字串。
/// </summary>
public class SampleWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SampleWebApplicationFactory(string connectionString)
    {
        this._connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SampleDb", this._connectionString);
        builder.UseEnvironment("IntegrationTest");
    }
}
