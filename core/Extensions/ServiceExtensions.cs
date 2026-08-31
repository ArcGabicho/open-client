using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenClient.Api;
using OpenClient.Data;
using OpenClient.Data.Repositories;
using OpenClient.Interfaces;
using OpenClient.Models.DTO.Users;
using OpenClient.Models.Validators;
using OpenClient.Services;
using OpenClient.Services.Api;

namespace OpenClient.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core: solo factory. Todos los consumidores abren su propio
        // contexto (repositorio, AuthService, DbInitializer), lo que es seguro
        // frente a la concurrencia de un circuito Blazor.
        services.AddDbContextFactory<OpenClientDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositorios
        services.AddScoped<IClientRepository, ClientRepository>();

        // Servicios de aplicación
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IDbInitializer, DbInitializer>();
        services.AddScoped<IContactMailer, SmtpContactMailer>();

        // Validadores (FluentValidation)
        services.AddValidatorsFromAssemblyContaining<ClientEditModelValidator>();

        return services;
    }

    public static IServiceCollection AddIntegrationApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IApiClientService, ApiClientService>();

        // Documento OpenAPI dedicado: solo los endpoints bajo api/v1.
        services.AddOpenApi(ApiV1.OpenApiDocumentName, options =>
        {
            options.ShouldInclude = description =>
                description.RelativePath?.StartsWith(
                    ApiV1.RoutePrefix, StringComparison.OrdinalIgnoreCase) == true;

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Open Client — API de Integración";
                document.Info.Version = "v1";
                document.Info.Description =
                    "API REST de solo lectura sobre la cartera comercial de clientes. " +
                    "Usa el sistema de sesión/autenticación existente de ASP.NET Core: " +
                    "todos los endpoints requieren un usuario autenticado y autorizado.";
                return Task.CompletedTask;
            });
        });

        // CORS preparado pero inactivo: se registra una política con nombre que lee
        // los orígenes desde configuración (Api:Cors:AllowedOrigins). Para activarla
        // basta poblar esa lista y añadir app.UseCors(ApiV1.CorsPolicy) al pipeline.
        var allowedOrigins = configuration
            .GetSection("Api:Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(ApiV1.CorsPolicy, policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }));

        return services;
    }

    // Módulo de Analíticas: servicio independiente + caché en memoria (opcional,
    // gobernada por Analytics:CacheSeconds; 0 = desactivada).
    public static IServiceCollection AddAnalytics(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        return services;
    }

    // Módulo de Usuarios: administración de cuentas del panel. Se apoya en el
    // sistema de autenticación/roles existente (entidad User + BCrypt + cookie);
    // no introduce identidad paralela. Los validadores FluentValidation del
    // ensamblado ya se registran en AddApplicationServices.
    public static IServiceCollection AddUserManagement(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserAuditLogger, UserAuditLogger>();
        return services;
    }

    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/log-in";
                options.AccessDeniedPath = "/access-denied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                options.Cookie.Name = ".OpenClient.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.IsEssential = true;

                // La API no debe redirigir peticiones no autenticadas hacia el HTML
                // de login: bajo /api responde con el código de estado correspondiente.
                options.Events.OnRedirectToLogin = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                // Un usuario desactivado (o eliminado) desde el módulo de Usuarios
                // no debe seguir accediendo con una cookie ya emitida. Se revalida
                // como máximo una vez cada pocos minutos para no golpear la BD en
                // cada request.
                options.Events.OnValidatePrincipal = ValidateStillActiveAsync;
            });

        services.AddAuthorization(options =>
        {
            // Reutiliza el claim de rol existente (ClaimTypes.Role). Preparado para,
            // más adelante, sumar API Keys como requisito alternativo de esta política.
            options.AddPolicy(ApiV1.ReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(ApiV1.AllowedRoles));

            // Módulo de Usuarios: solo administradores, validado en backend.
            options.AddPolicy(UserRoles.AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoles.Admin));
        });

        return services;
    }

    private static readonly TimeSpan RevalidateEvery = TimeSpan.FromMinutes(3);

    private static async Task ValidateStillActiveAsync(CookieValidatePrincipalContext context)
    {
        const string checkedAtKey = "users:validated_at";

        var now = DateTimeOffset.UtcNow;
        if (context.Properties.Items.TryGetValue(checkedAtKey, out var raw)
            && DateTimeOffset.TryParse(raw, out var last)
            && now - last < RevalidateEvery)
        {
            return;
        }

        var idText = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idText, out var userId))
        {
            return;
        }

        var factory = context.HttpContext.RequestServices
            .GetRequiredService<IDbContextFactory<OpenClientDbContext>>();

        await using var db = await factory.CreateDbContextAsync(context.HttpContext.RequestAborted);

        var stillActive = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, context.HttpContext.RequestAborted);

        if (!stillActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        context.Properties.Items[checkedAtKey] = now.ToString("o");
        context.ShouldRenew = true;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    private static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
