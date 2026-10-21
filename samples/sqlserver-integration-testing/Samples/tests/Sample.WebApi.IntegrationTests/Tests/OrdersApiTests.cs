using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Sample.WebApi.IntegrationTests.Fixtures;
using Sample.WebApi.Models;

namespace Sample.WebApi.IntegrationTests.Tests;

public class OrdersApiTests : IntegrationTestBase
{
    public OrdersApiTests(SqlServerContainerFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(-1, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, -1, "pageSize")]
    [InlineData(1, 201, "pageSize")]
    [InlineData(1, int.MaxValue, "pageSize")]
    public async Task GetByShipper_分頁參數不合法_應回傳400與欄位錯誤(int page, int pageSize, string field)
    {
        // act
        using var response = await this.Client.GetAsync(
            $"/api/orders/by-shipper/1?page={page}&pageSize={pageSize}",
            TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem.Status.Should().Be(400);
        problem.Errors.Should().ContainKey(field);
    }

    [Fact]
    public async Task GetByShipper_頁碼為Int最大值_應回傳空集合而非溢位()
    {
        // act
        var actual = await this.Client.GetFromJsonAsync<List<Order>>(
            $"/api/orders/by-shipper/1?page={int.MaxValue}&pageSize=200",
            TestContext.Current.CancellationToken);

        // assert
        actual.Should().NotBeNull();
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByShipper_每頁200筆_應允許上限並只回傳指定物流商資料()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "查詢物流");
        var otherShipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "其他物流");
        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString,
        [
            new Order { ShipperId = shipperId, CustomerName = "目標客戶", OrderDate = DateTime.UtcNow },
            new Order { ShipperId = otherShipperId, CustomerName = "其他客戶", OrderDate = DateTime.UtcNow }
        ]);

        // act
        var actual = await this.Client.GetFromJsonAsync<List<Order>>(
            $"/api/orders/by-shipper/{shipperId}?pageSize=200",
            TestContext.Current.CancellationToken);

        // assert
        actual.Should().NotBeNull();
        actual.Should().ContainSingle().Which.CustomerName.Should().Be("目標客戶");
        actual.Should().OnlyContain(x => x.ShipperId == shipperId);
    }
}
