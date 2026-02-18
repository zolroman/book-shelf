using Bookshelf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bookshelf.Api.Api.HealthChecks;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly BookshelfDbContext _dbContext;

    public DatabaseReadinessHealthCheck(BookshelfDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is available.");
            }

            return HealthCheckResult.Unhealthy("Database connection check returned false.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", exception);
        }
    }
}
