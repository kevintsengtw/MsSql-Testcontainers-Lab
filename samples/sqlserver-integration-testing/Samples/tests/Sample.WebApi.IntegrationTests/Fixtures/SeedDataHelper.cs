using Dapper;
using Microsoft.Data.SqlClient;
using Sample.WebApi.Models;

namespace Sample.WebApi.IntegrationTests.Fixtures;

/// <summary>
/// 測試資料建立 helper。所有 seed 操作統一經過這裡，好處是 commandTimeout 有一致的預設值：
/// 120 秒，因為容器啟動初期本來就是最慢的時刻。呼叫端可用 commandTimeout 參數覆寫。
/// </summary>
public static class SeedDataHelper
{
    private const int SeedCommandTimeout = 120;

    public static async Task<int> InsertShipperAsync(
        string connectionString, string companyName, string? phone = null, int? commandTimeout = null)
    {
        const string sql =
            """
            INSERT INTO dbo.Shippers (CompanyName, Phone)
            VALUES (@CompanyName, @Phone);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = new SqlConnection(connectionString);

        return await connection.ExecuteScalarAsync<int>(
            sql,
            new { CompanyName = companyName, Phone = phone },
            commandTimeout: commandTimeout ?? SeedCommandTimeout);
    }

    public static async Task InsertOrdersAsync(
        string connectionString, IEnumerable<Order> orders, int? commandTimeout = null)
    {
        const string sql =
            """
            INSERT INTO dbo.Orders (ShipperID, CustomerName, Freight, OrderDate)
            VALUES (@ShipperId, @CustomerName, @Freight, @OrderDate);
            """;

        await using var connection = new SqlConnection(connectionString);

        await connection.ExecuteAsync(
            sql, orders, commandTimeout: commandTimeout ?? SeedCommandTimeout);
    }
}
