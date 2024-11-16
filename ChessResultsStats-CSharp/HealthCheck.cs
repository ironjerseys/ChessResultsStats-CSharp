using ChessResultsStats_CSharp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChessResultsStats_CSharp;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ChessGamesDbContext _dbContext;

    public DatabaseHealthCheck(ChessGamesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Tentative d'accès à la base de données
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("Database connection is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection is unhealthy.", ex);
        }
    }
}
