using Microsoft.EntityFrameworkCore;
using OpenClient.Models.Domain;

namespace OpenClient.Data;

public sealed class DbInitializer
{
    private readonly OpenClientDbContext _db;
    private readonly ILogger<DbInitializer> _logger;
    private readonly IConfiguration _configuration;

    public DbInitializer(
        OpenClientDbContext db,
        ILogger<DbInitializer> logger,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando base de datos...");

        await ApplyMigrationsAsync(ct);

        await SeedAdminAsync(ct);

        await SeedClientsAsync(ct);

        _logger.LogInformation("Base de datos inicializada correctamente.");
    }

    private async Task ApplyMigrationsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Aplicando migraciones de EF Core...");

        var pending = await _db.Database.GetPendingMigrationsAsync(ct);

        if (pending.Any())
        {
            _logger.LogInformation(
                "Migraciones pendientes: {Count}",
                pending.Count());

            await _db.Database.MigrateAsync(ct);

            _logger.LogInformation("Migraciones aplicadas correctamente.");
        }
        else
        {
            _logger.LogInformation("No hay migraciones pendientes.");
        }
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        var adminEmail = _configuration["ADMIN_EMAIL"];
        var adminPassword = _configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning(
                "ADMIN_EMAIL o ADMIN_PASSWORD no definidos. " +
                "Se omite el provisionamiento del administrador.");

            return;
        }

        _logger.LogInformation(
            "Verificando administrador inicial...");

        var existing = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == adminEmail, ct);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Administrador ya existe (Email={Email}). " +
                "No se sobrescribe el hash existente.",
                adminEmail);

            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12);

        var admin = new User
        {
            Email = adminEmail,
            PasswordHash = hash,
            Role = "Admin",
            FirstName = "Admin",
            LastName = "User",
            ProfileImage = "",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(admin);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Administrador creado correctamente (Email={Email}).",
            adminEmail);
    }

    private async Task SeedClientsAsync(CancellationToken ct)
    {
        var seeder = new DbSeeder(_db, _logger);
        await seeder.SeedClientsAsync(ct);
    }
}