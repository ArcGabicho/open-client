using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenClient.Data;
using OpenClient.Interfaces;

namespace OpenClient.Api.Tests.Infrastructure;

/// <summary>
/// Arranca la aplicación real en memoria, pero sustituye SQL Server por SQLite
/// en memoria (conexión compartida abierta durante toda la vida de la fábrica) y
/// el inicializador de base de datos por uno que solo crea el esquema. La
/// autenticación se resuelve con <see cref="TestAuthHandler"/>; la cookie sigue
/// siendo el esquema de reto/forbid, de modo que se comprueba el 401/403 real.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveDbRegistrations(services);

            services.AddDbContextFactory<OpenClientDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IDbInitializer>();
            services.AddScoped<IDbInitializer, SchemaOnlyInitializer>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }

    private static void RemoveDbRegistrations(IServiceCollection services)
    {
        var toRemove = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions) ||
                (descriptor.ServiceType.FullName?.Contains(nameof(OpenClientDbContext)) ?? false) ||
                (descriptor.ServiceType.FullName?.Contains("DbContextOptions") ?? false))
            .ToList();

        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

/// <summary>Crea el esquema en SQLite y no siembra nada; cada prueba controla sus datos.</summary>
internal sealed class SchemaOnlyInitializer : IDbInitializer
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;

    public SchemaOnlyInitializer(IDbContextFactory<OpenClientDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
