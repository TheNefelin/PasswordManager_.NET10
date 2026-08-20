using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApiCore.Infrastructure.Data;

namespace WebApiCore.Health;

public class SqlHealthCheck : IHealthCheck
{
    private readonly IDapperContext _dapper;

    public SqlHealthCheck(IDapperContext dapper)
    {
        _dapper = dapper;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _dapper.CreateConnection();
            await connection.ExecuteScalarAsync<int>("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Base de datos no disponible.", ex);
        }
    }
}