using JobFree.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace JobFree.Infrastructure.Health;

/// <summary>
/// Kiểm tra trạng thái hoạt động (Health Check) của các phụ thuộc hạ tầng cơ bản bao gồm Database (PostgreSQL) và Cache (Redis).
/// </summary>
public sealed class DependencyHealthCheck(
    JobFreeDbContext database,
    IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Database is unavailable.");
            }

            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Do not expose provider exceptions, connection strings or credentials in health output.
            return HealthCheckResult.Unhealthy("Database or cache is unavailable.");
        }
    }
}
