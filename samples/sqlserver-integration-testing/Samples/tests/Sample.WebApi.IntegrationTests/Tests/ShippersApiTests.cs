using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sample.WebApi.IntegrationTests.Fixtures;
using Sample.WebApi.Models;

namespace Sample.WebApi.IntegrationTests.Tests;

/// <summary>
/// 場景一：透過 WebApplicationFactory 從 HTTP 層驗證 API 完整行為，
/// 資料真實落在 Testcontainers 的 SQL Server。
/// </summary>
public class ShippersApiTests : IntegrationTestBase
{
    public ShippersApiTests(SqlServerContainerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetAll_資料庫沒有資料_應回傳空集合()
    {
        // act
        var actual = await this.Client.GetFromJsonAsync<List<Shipper>>(
            "/api/shippers", TestContext.Current.CancellationToken);

        // assert
        actual.Should().NotBeNull();
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_輸入合法資料_應回傳201並可由GetById取回()
    {
        // arrange
        var shipper = new Shipper { CompanyName = "貓頭鷹物流", Phone = "(02) 1234-5678" };

        // act
        using var response = await this.Client.PostAsJsonAsync(
            "/api/shippers", shipper, TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Shipper>(TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created.ShipperId.Should().BeGreaterThan(0);

        var fetched = await this.Client.GetFromJsonAsync<Shipper>(
            $"/api/shippers/{created.ShipperId}", TestContext.Current.CancellationToken);
        fetched.Should().NotBeNull();
        fetched.CompanyName.Should().Be("貓頭鷹物流");
    }

    [Fact]
    public async Task Create_CompanyName重複_應回傳409()
    {
        // arrange：直接 seed 一筆既有資料
        await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "重複物流");

        var duplicated = new Shipper { CompanyName = "重複物流" };

        // act
        using var response = await this.Client.PostAsJsonAsync(
            "/api/shippers", duplicated, TestContext.Current.CancellationToken);

        // assert
        await AssertConflictAsync(response, "公司名稱已存在", "/api/shippers");
    }

    [Fact]
    public async Task Update_指定的資料不存在_應回傳404()
    {
        // arrange
        var shipper = new Shipper { CompanyName = "不存在物流" };

        // act
        using var response = await this.Client.PutAsJsonAsync(
            "/api/shippers/9999", shipper, TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/problem+json", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/xml", false)]
    public async Task Create_公司名稱重複且指定Accept_應維持409(string accept, bool expectsProblemDetails)
    {
        // arrange
        await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "格式協商物流");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shippers")
        {
            Content = JsonContent.Create(new Shipper { CompanyName = "格式協商物流" })
        };
        request.Headers.Accept.ParseAdd(accept);

        // act
        using var response = await this.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        if (expectsProblemDetails)
        {
            await AssertConflictAsync(response, "公司名稱已存在", "/api/shippers");
        }
        else
        {
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }

        await using var connection = this.CreateConnection();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(1);
    }

    [Fact]
    public async Task Delete_指定的資料存在_應回傳204且資料被刪除()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(
            this.ConnectionString, "待刪除物流");

        // act
        using var deleteResponse = await this.Client.DeleteAsync($"/api/shippers/{shipperId}", TestContext.Current.CancellationToken);
        using var getResponse = await this.Client.GetAsync($"/api/shippers/{shipperId}", TestContext.Current.CancellationToken);

        // assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("POST", "CompanyName", 0)]
    [InlineData("POST", "CompanyName", -1)]
    [InlineData("POST", "CompanyName", 41)]
    [InlineData("POST", "Phone", 25)]
    [InlineData("PUT", "CompanyName", 0)]
    [InlineData("PUT", "CompanyName", -1)]
    [InlineData("PUT", "CompanyName", 41)]
    [InlineData("PUT", "Phone", 25)]
    public async Task Save_欄位不合法_應回傳400且不寫入資料(string method, string field, int length)
    {
        // arrange
        var shipperId = method == "PUT"
            ? await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "原有物流")
            : 0;
        var shipper = new Shipper { CompanyName = "合法物流" };
        var invalidValue = length < 0 ? "   " : new string('X', length);
        if (field == "CompanyName")
        {
            shipper.CompanyName = invalidValue;
        }
        else
        {
            shipper.Phone = invalidValue;
        }

        var path = method == "POST" ? "/api/shippers" : $"/api/shippers/{shipperId}";
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(shipper)
        };

        // act
        using var response = await this.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // assert
        await AssertValidationAsync(response, field);
        await using var connection = this.CreateConnection();
        if (method == "POST")
        {
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(0);
        }
        else
        {
            (await connection.ExecuteScalarAsync<string>(
                "SELECT CompanyName FROM dbo.Shippers WHERE ShipperID = @Id;", new { Id = shipperId }))
                .Should().Be("原有物流");
        }
    }

    [Theory]
    [InlineData("POST", "{}")]
    [InlineData("POST", "{\"companyName\":null}")]
    [InlineData("PUT", "{}")]
    [InlineData("PUT", "{\"companyName\":null}")]
    public async Task Save_公司名稱缺少或為Null_應回傳400(string method, string body)
    {
        // arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shippers" + (method == "PUT" ? "/1" : ""))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        // act
        using var response = await this.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // assert
        await AssertValidationAsync(response, "CompanyName");
    }

    [Fact]
    public async Task Create_欄位長度恰為上限_應回傳201並保存完整內容()
    {
        // arrange
        var shipper = new Shipper { CompanyName = new string('X', 40), Phone = new string('1', 24) };

        // act
        using var response = await this.Client.PostAsJsonAsync("/api/shippers", shipper, TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<Shipper>(TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        var fetched = await this.Client.GetFromJsonAsync<Shipper>(
            $"/api/shippers/{created.ShipperId}", TestContext.Current.CancellationToken);
        fetched.Should().NotBeNull();
        fetched.CompanyName.Should().Be(shipper.CompanyName);
        fetched.Phone.Should().Be(shipper.Phone);
    }

    [Fact]
    public async Task Update_指定資料存在_應回傳204並保存新內容()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "更新前物流");
        var shipper = new Shipper { CompanyName = "更新後物流", Phone = "02-12345678" };

        // act
        using var response = await this.Client.PutAsJsonAsync(
            $"/api/shippers/{shipperId}", shipper, TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await this.Client.GetFromJsonAsync<Shipper>(
            $"/api/shippers/{shipperId}", TestContext.Current.CancellationToken);
        fetched.Should().NotBeNull();
        fetched.CompanyName.Should().Be(shipper.CompanyName);
        fetched.Phone.Should().Be(shipper.Phone);
    }

    [Fact]
    public async Task Update_公司名稱與其他資料重複_應回傳409並保留原值()
    {
        // arrange
        await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "既有物流");
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "待更新物流");
        var path = $"/api/shippers/{shipperId}";

        // act
        using var response = await this.Client.PutAsJsonAsync(
            path, new Shipper { CompanyName = "既有物流" }, TestContext.Current.CancellationToken);

        // assert
        await AssertConflictAsync(response, "公司名稱已存在", path);
        var fetched = await this.Client.GetFromJsonAsync<Shipper>(path, TestContext.Current.CancellationToken);
        fetched.Should().NotBeNull();
        fetched.CompanyName.Should().Be("待更新物流");
    }

    [Fact]
    public async Task Delete_指定資料不存在_應回傳404()
    {
        // act
        using var response = await this.Client.DeleteAsync("/api/shippers/9999", TestContext.Current.CancellationToken);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_仍有訂單參照_應回傳409且保留資料()
    {
        // arrange
        var shipperId = await SeedDataHelper.InsertShipperAsync(this.ConnectionString, "訂單物流");
        await SeedDataHelper.InsertOrdersAsync(this.ConnectionString,
        [
            new Order { ShipperId = shipperId, CustomerName = "訂單客戶", OrderDate = DateTime.UtcNow }
        ]);
        var path = $"/api/shippers/{shipperId}";

        // act
        using var response = await this.Client.DeleteAsync(path, TestContext.Current.CancellationToken);

        // assert
        await AssertConflictAsync(response, "資料關聯限制衝突", path);
        await using var connection = this.CreateConnection();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Orders;")).Should().Be(1);
    }

    [Fact]
    public async Task Create_同時建立相同公司名稱_應一筆成功另一筆回傳409()
    {
        // arrange
        var shipper = new Shipper { CompanyName = "同時建立物流" };

        // act
        var responses = await Task.WhenAll(
            this.Client.PostAsJsonAsync("/api/shippers", shipper, TestContext.Current.CancellationToken),
            this.Client.PostAsJsonAsync("/api/shippers", shipper, TestContext.Current.CancellationToken));
        using var first = responses[0];
        using var second = responses[1];

        // assert
        responses.Select(x => x.StatusCode).Should().BeEquivalentTo(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        await AssertConflictAsync(responses.Single(x => x.StatusCode == HttpStatusCode.Conflict), "公司名稱已存在", "/api/shippers");
        await using var connection = this.CreateConnection();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Shippers;")).Should().Be(1);
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem.Status.Should().Be(400);
        problem.Errors.Should().ContainKey(field);
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response, string title, string path)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem.Status.Should().Be(409);
        problem.Title.Should().Be(title);
        problem.Instance.Should().Be(path);
        problem.Detail.Should().NotBeNullOrWhiteSpace();
        problem.Detail.Should().NotContain("dbo.");
    }
}
