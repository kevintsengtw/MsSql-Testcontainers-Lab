using AwesomeAssertions;
using Dapper;
using Microsoft.Data.SqlClient;
using Sample.WebApi.Infrastructure;
using Sample.WebApi.IntegrationTests.Fixtures;
using Sample.WebApi.Models;
using Sample.WebApi.Repositories;

namespace Sample.WebApi.IntegrationTests.Tests;

/// <summary>
/// 在真實資料庫驗證交易提交、外鍵錯誤與全部回復。
/// </summary>
public class OrderRepositoryTransactionTests : IntegrationTestBase
{
    public OrderRepositoryTransactionTests(SqlServerContainerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task CreateBatchAsync_全部合法_應全數寫入()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(
            this.ConnectionString, "交易測試物流");
        var repository = this.CreateRepository();

        var orders = Enumerable.Range(1, 10)
                               .Select(i => new Order
                               {
                                   ShipperId = shipperId,
                                   CustomerName = $"客戶 {i}",
                                   Freight = i * 10m,
                                   OrderDate = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)
                               }).ToArray();

        // act
        var affected = await repository.CreateBatchAsync(orders);

        // assert
        affected.Should().Be(10);
        await using var connection = this.CreateConnection();
        var stored = await connection.QueryAsync<Order>(
            """
            SELECT OrderID AS OrderId, ShipperID AS ShipperId, CustomerName, Freight, OrderDate
            FROM dbo.Orders ORDER BY OrderID;
            """);
        stored.Should().BeEquivalentTo(orders,
            options => options.Excluding(x => x.OrderId).WithStrictOrdering());
    }

    [Fact]
    public async Task CreateBatchAsync_其中一筆違反FK_應全數回復()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(
            this.ConnectionString, "回復測試物流");
        var repository = this.CreateRepository();

        var orders = new List<Order>
        {
            new() { ShipperId = shipperId, CustomerName = "合法客戶", Freight = 10m, OrderDate = DateTime.UtcNow },
            new() { ShipperId = 9999, CustomerName = "FK 違規客戶", Freight = 20m, OrderDate = DateTime.UtcNow }
        };

        // act
        var act = async () => await repository.CreateBatchAsync(orders);

        // assert：例外要拋出，且「合法的那一筆」也必須不存在（交易原子性）
        var exception = await act.Should().ThrowAsync<SqlException>();
        exception.Which.Number.Should().Be(547);

        await using var connection = this.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Orders");
        count.Should().Be(0);
    }

    private OrderRepository CreateRepository()
    {
        return new OrderRepository(new SqlConnectionFactory(this.ConnectionString));
    }
}
