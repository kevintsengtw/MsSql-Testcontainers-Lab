using AwesomeAssertions;
using Dapper;
using Sample.WebApi.IntegrationTests.Fixtures;
using Sample.WebApi.Models;

namespace Sample.WebApi.IntegrationTests.InfrastructureTests;

public class DatabaseResetTests : IntegrationTestBase
{
    private readonly SqlServerContainerFixture _fixture;

    public DatabaseResetTests(SqlServerContainerFixture fixture) : base(fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task ResetDatabase_已有關聯資料_應清空兩張表並重設Identity()
    {
        // arrange：在同一測試內寫入資料，不依賴其他測試的執行順序。
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "重設前物流");
        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString,
        [
            new Order { ShipperId = shipperId, CustomerName = "重設前客戶", OrderDate = DateTime.UtcNow }
        ]);
        await using var connection = this.CreateConnection();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders;")).Should().Be(1);

        // act
        await this._fixture.ResetDatabaseAsync();

        // assert
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(0);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders;")).Should().Be(0);
        var newShipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "重設後物流");
        newShipperId.Should().Be(1);
        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString,
        [
            new Order { ShipperId = newShipperId, CustomerName = "重設後客戶", OrderDate = DateTime.UtcNow }
        ]);
        (await connection.ExecuteScalarAsync<int>("SELECT OrderID FROM dbo.Orders;")).Should().Be(1);
    }
}
