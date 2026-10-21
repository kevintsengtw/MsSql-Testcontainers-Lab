using AwesomeAssertions;
using Dapper;
using Sample.WebApi.IntegrationTests.Fixtures;

namespace Sample.WebApi.IntegrationTests.InfrastructureTests;

/// <summary>
/// 釘住 SQL Server on Linux 的目錄語意，避免把 /var/opt/mssql/log
/// 誤認成資料庫交易記錄檔（.ldf）的預設目錄。
/// </summary>
public class SqlServerStorageLayoutTests : IntegrationTestBase
{
    public SqlServerStorageLayoutTests(SqlServerContainerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task SampleDb_資料檔與交易記錄檔_預設都應位於Data目錄()
    {
        await using var connection = this.CreateConnection();

        var files = (await connection.QueryAsync<DatabaseFile>(
            """
            SELECT type_desc AS TypeDescription,
                   physical_name AS PhysicalName
            FROM sys.database_files
            ORDER BY file_id;
            """)).AsList();

        files.Should().ContainSingle(x => x.TypeDescription == "ROWS");
        files.Should().ContainSingle(x => x.TypeDescription == "LOG");
        files.Should().OnlyContain(
            x => x.PhysicalName.StartsWith("/var/opt/mssql/data/", StringComparison.Ordinal),
            "/var/opt/mssql/data 預設同時存放 .mdf/.ndf 與 .ldf");
    }

    [Fact]
    public async Task SqlServerErrorLog_應位於Log目錄且不是資料庫交易記錄檔()
    {
        await using var connection = this.CreateConnection();

        var errorLogPath = await connection.ExecuteScalarAsync<string>(
            "SELECT CAST(SERVERPROPERTY('ErrorLogFileName') AS nvarchar(4000));");

        errorLogPath.Should().StartWith(
            "/var/opt/mssql/log/",
            "/var/opt/mssql/log 是 error log、Extended Events 與 dump 等診斷資料的目錄");
    }

    private sealed record DatabaseFile(string TypeDescription, string PhysicalName);
}
