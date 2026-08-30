using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenClient.Data;
using OpenClient.Models.Domain;
using Xunit;

namespace OpenClient.Api.Tests.Infrastructure;

/// <summary>
/// Base de las pruebas de la API: comparte una <see cref="ApiFactory"/> por clase y
/// deja la tabla <c>Clients</c> vacía antes de cada prueba.
/// </summary>
public abstract class ApiTestBase : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
    }

    protected ApiFactory Factory { get; }

    public async Task InitializeAsync()
    {
        await using var db = await CreateDbAsync();
        await db.Database.EnsureCreatedAsync();
        db.Clients.RemoveRange(db.Clients);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<OpenClientDbContext> CreateDbAsync() =>
        Factory.Services
            .GetRequiredService<IDbContextFactory<OpenClientDbContext>>()
            .CreateDbContextAsync();

    protected async Task SeedAsync(params Client[] clients)
    {
        await using var db = await CreateDbAsync();
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();
    }

    protected async Task SeedUsersAsync(params User[] users)
    {
        await using var db = await CreateDbAsync();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }

    protected async Task<User?> ReloadUserAsync(int id)
    {
        await using var db = await CreateDbAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>Cliente HTTP autenticado (rol <c>Admin</c> salvo que se indique otra cosa).</summary>
    protected HttpClient CreateAuthenticatedClient(string roles = "Admin", int? userId = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "tester@example.com");

        if (!string.IsNullOrEmpty(roles))
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        }

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.Value.ToString());
        }

        return client;
    }

    /// <summary>Cliente HTTP sin credenciales.</summary>
    protected HttpClient CreateAnonymousClient() => Factory.CreateClient();

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(value);
        return value!;
    }

    protected async Task<T> GetJsonAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<T>(response);
    }
}
