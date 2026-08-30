using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Data.Repositories;
using OpenClient.Interfaces;
using OpenClient.Models.Validators;
using OpenClient.Services;

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

        // Validadores (FluentValidation)
        services.AddValidatorsFromAssemblyContaining<ClientEditModelValidator>();

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
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
