using System.Data;
using Microsoft.Data.SqlClient;

namespace Sample.WebApi.Infrastructure;

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        this._connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(this._connectionString);
    }
}
