using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OpenClient.Data;

public sealed class DbHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;

    public DbHealthCheck(IDbContextFactory<OpenClientDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Conexión a SQL Server correcta.")
                : HealthCheckResult.Unhealthy("SQL Server no acepta conexiones.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error consultando SQL Server.", ex);
        }
    }
}