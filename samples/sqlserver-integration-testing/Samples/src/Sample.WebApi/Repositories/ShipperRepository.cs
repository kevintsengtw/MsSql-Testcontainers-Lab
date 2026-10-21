using Dapper;
using Sample.WebApi.Infrastructure;
using Sample.WebApi.Models;

namespace Sample.WebApi.Repositories;

public class ShipperRepository : IShipperRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ShipperRepository(IDbConnectionFactory connectionFactory)
    {
        this._connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Shipper>> GetAllAsync()
    {
        const string sql =
            """
            SELECT ShipperID AS ShipperId, CompanyName, Phone
            FROM dbo.Shippers
            ORDER BY ShipperID;
            """;

        using var connection = this._connectionFactory.CreateConnection();
        return await connection.QueryAsync<Shipper>(sql);
    }

    public async Task<Shipper?> GetByIdAsync(int shipperId)
    {
        const string sql =
            """
            SELECT ShipperID AS ShipperId, CompanyName, Phone
            FROM dbo.Shippers
            WHERE ShipperID = @ShipperId;
            """;

        using var connection = this._connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Shipper>(sql, new { ShipperId = shipperId });
    }

    public async Task<int> CreateAsync(Shipper shipper)
    {
        const string sql =
            """
            INSERT INTO dbo.Shippers (CompanyName, Phone)
            VALUES (@CompanyName, @Phone);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var connection = this._connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, shipper);
    }

    public async Task<bool> UpdateAsync(Shipper shipper)
    {
        const string sql =
            """
            UPDATE dbo.Shippers
            SET CompanyName = @CompanyName, Phone = @Phone
            WHERE ShipperID = @ShipperId;
            """;

        using var connection = this._connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, shipper);
        return affected is 1;
    }

    public async Task<bool> DeleteAsync(int shipperId)
    {
        const string sql =
            """
            DELETE FROM dbo.Shippers
            WHERE ShipperID = @ShipperId;
            """;

        using var connection = this._connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { ShipperId = shipperId });
        return affected is 1;
    }

}
