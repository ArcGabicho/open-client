using Microsoft.EntityFrameworkCore;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;

namespace OpenClient.Data;

/// <inheritdoc cref="IDbInitializer" />
public sealed class DbInitializer : IDbInitializer
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;
    private readonly ILogger<DbInitializer> _logger;
    private readonly IConfiguration _configuration;

    public DbInitializer(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        ILogger<DbInitializer> logger,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando base de datos...");

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await ApplyMigrationsAsync(db, cancellationToken);
        await SeedAdminAsync(db, cancellationToken);
        await SeedClientsAsync(db, cancellationToken);

        _logger.LogInformation("Base de datos inicializada correctamente.");
    }

    private async Task ApplyMigrationsAsync(OpenClientDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Aplicando migraciones de EF Core...");

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pending.Count > 0)
        {
            _logger.LogInformation("Migraciones pendientes: {Count}", pending.Count);
            await db.Database.MigrateAsync(ct);
            _logger.LogInformation("Migraciones aplicadas correctamente.");
        }
        else
        {
            _logger.LogInformation("No hay migraciones pendientes.");
        }
    }

    private async Task SeedAdminAsync(OpenClientDbContext db, CancellationToken ct)
    {
        var adminEmail = _configuration["ADMIN_EMAIL"];
        var adminPassword = _configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning(
                "ADMIN_EMAIL o ADMIN_PASSWORD no definidos. " +
                "Se omite el provisionamiento del administrador.");
            return;
        }

        _logger.LogInformation("Verificando administrador inicial...");

        var existing = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == adminEmail, ct);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Administrador ya existe (Email={Email}). No se sobrescribe el hash existente.",
                adminEmail);
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12);

        db.Users.Add(new User
        {
            Email = adminEmail,
            PasswordHash = hash,
            Role = "Admin",
            FirstName = "Admin",
            LastName = "User",
            ProfileImage = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Administrador creado correctamente (Email={Email}).", adminEmail);
    }

    private async Task SeedClientsAsync(OpenClientDbContext db, CancellationToken ct)
    {
        var seeder = new DbSeeder(db, _logger);
        await seeder.SeedClientsAsync(ct);
    }
}
