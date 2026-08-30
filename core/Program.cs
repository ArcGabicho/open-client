using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using openclient.Components;
using OpenClient.Api;
using OpenClient.Extensions;
using OpenClient.Interfaces;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging (Serilog) ----------
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/openclient-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7);
});

// ---------- Servicios ----------
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Expone el estado de autenticación a los componentes (página de Usuarios).
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCookieAuthentication();
builder.Services.AddObservability();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Módulo independiente de API de integración (/api/v1).
builder.Services.AddIntegrationApi(builder.Configuration);

// Módulo independiente de Analíticas (/api/analytics + página /dashboard/analytics).
builder.Services.AddAnalytics();

// Módulo de Usuarios (/api/users + página /dashboard/users).
builder.Services.AddUserManagement();

var app = builder.Build();

// ---------- Inicialización de la base de datos ----------
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    await initializer.InitializeAsync();
}

// ---------- Pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();

// Da formato JSON homogéneo a los errores de /api/v1 (incluidos 401/403) y evita
// filtrar detalles internos. Debe ir antes de la autenticación.
app.UseApiErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapStaticAssets();
app.MapControllers();

// Documento OpenAPI de la API v1: /openapi/v1.json
app.MapOpenApi();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Punto de extensión para pruebas de integración (WebApplicationFactory<Program>).
public partial class Program;