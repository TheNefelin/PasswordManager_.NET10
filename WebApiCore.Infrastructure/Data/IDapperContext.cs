using System.Data;

namespace WebApiCore.Infrastructure.Data;

public interface IDapperContext
{
    IDbConnection CreateConnection();
}