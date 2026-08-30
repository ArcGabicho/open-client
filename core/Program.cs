using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using openclient.Components;
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

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCookieAuthentication();
builder.Services.AddObservability();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
