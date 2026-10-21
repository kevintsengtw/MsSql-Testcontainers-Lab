using System.Data;
using Dapper;
using Sample.WebApi.Infrastructure;
using Sample.WebApi.Models;

namespace Sample.WebApi.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        this._connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Order>> GetByShipperAsync(int shipperId, int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        var offset = ((long)page - 1) * pageSize;

        const string sql =
            """
            SELECT OrderID AS OrderId, ShipperID AS ShipperId,
                   CustomerName, Freight, OrderDate
            FROM dbo.Orders
            WHERE ShipperID = @ShipperId
            ORDER BY OrderID
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = this._connectionFactory.CreateConnection();
        return await connection.QueryAsync<Order>(
            sql, new { ShipperId = shipperId, Offset = offset, PageSize = pageSize });
    }

    public async Task<int> CreateBatchAsync(IEnumerable<Order> orders)
    {
        const string sql =
            """
            INSERT INTO dbo.Orders (ShipperID, CustomerName, Freight, OrderDate)
            VALUES (@ShipperId, @CustomerName, @Freight, @OrderDate);
            """;

        using var connection = this._connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var affected = await connection.ExecuteAsync(sql, orders, transaction);
            transaction.Commit();
            return affected;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
