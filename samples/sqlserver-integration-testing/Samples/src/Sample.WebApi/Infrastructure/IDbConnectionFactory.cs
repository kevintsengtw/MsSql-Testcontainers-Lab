using System.Data;

namespace Sample.WebApi.Infrastructure;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
