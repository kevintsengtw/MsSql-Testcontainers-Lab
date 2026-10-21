using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Dapper;
using Sample.WebApi.IntegrationTests.Fixtures;
using Sample.WebApi.Models;

namespace Sample.WebApi.IntegrationTests.Tests;

/// <summary>
/// 大量逐筆 seed 與分頁查詢，驗證資料筆數、排序及頁面邊界。
/// </summary>
public class OrdersBulkSeedTests : IntegrationTestBase
{
    private readonly IFixture _autoFixture = new Fixture();

    public OrdersBulkSeedTests(SqlServerContainerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetByShipper_塞入2000筆訂單_分頁查詢應正確()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(
            this.ConnectionString, "量販物流");

        var orders = this._autoFixture.Build<Order>()
                         .Without(x => x.OrderId)
                         .With(x => x.ShipperId, shipperId)
                         .With(x => x.OrderDate, DateTime.UtcNow)
                         .With(x => x.Freight, 99.5m)
                         .CreateMany(2000).ToArray();

        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString, orders);

        // act
        var page3 = await this.Client.GetFromJsonAsync<List<Order>>(
            $"/api/orders/by-shipper/{shipperId}?page=3&pageSize=50",
            TestContext.Current.CancellationToken);

        // assert
        page3.Should().NotBeNull();
        page3.Should().HaveCount(50);
        page3.Should().OnlyContain(x => x.ShipperId == shipperId);

        await using var connection = this.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Orders WHERE ShipperID = @ShipperId",
            new { ShipperId = shipperId });
        total.Should().Be(2000);
        var orderedIds = await connection.QueryAsync<int>(
            "SELECT OrderID FROM dbo.Orders WHERE ShipperID = @ShipperId ORDER BY OrderID;",
            new { ShipperId = shipperId });
        page3.Select(x => x.OrderId).Should().Equal(orderedIds.Skip(100).Take(50));
        page3.Select(x => x.CustomerName).Should().Equal(orders.Skip(100).Take(50).Select(x => x.CustomerName));
    }

    [Fact]
    public async Task GetByShipper_最後一頁不足50筆_應回傳剩餘資料且下一頁為空()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "末頁物流");
        var orders = Enumerable.Range(1, 120).Select(i => new Order
        {
            ShipperId = shipperId, CustomerName = $"客戶 {i}", OrderDate = DateTime.UtcNow
        }).ToArray();
        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString, orders);

        // act
        var lastPage = await this.Client.GetFromJsonAsync<List<Order>>(
            $"/api/orders/by-shipper/{shipperId}?page=3&pageSize=50", TestContext.Current.CancellationToken);
        var nextPage = await this.Client.GetFromJsonAsync<List<Order>>(
            $"/api/orders/by-shipper/{shipperId}?page=4&pageSize=50", TestContext.Current.CancellationToken);

        // assert
        lastPage.Should().NotBeNull();
        lastPage.Should().HaveCount(20);
        lastPage.Select(x => x.CustomerName).Should().Equal(orders.Skip(100).Select(x => x.CustomerName));
        nextPage.Should().NotBeNull();
        nextPage.Should().BeEmpty();
    }
}
