using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersSecretsTests : ApiTestBase
{
    public UsersSecretsTests(ApiFactory factory) : base(factory)
    {
    }

    private HttpClient Admin() => CreateAuthenticatedClient(roles: "Admin", userId: 1);

    private static void AssertNoSecrets(string json)
    {
        foreach (var forbidden in new[]
                 {
                     "passwordhash", "\"password\"", "securitystamp",
                     "recoverycode", "resettoken", "$2a$", "$2b$"
                 })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task List_response_carries_no_secrets()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2), UserFactory.Create(3));

        var json = await Admin().GetStringAsync("/api/users");

        AssertNoSecrets(json);
    }

    [Fact]
    public async Task Detail_response_carries_no_secrets()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var json = await Admin().GetStringAsync("/api/users/2");

        AssertNoSecrets(json);
    }

    [Fact]
    public async Task Create_response_carries_no_secrets()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await Admin().PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            UserName = "ada",
            Email = "ada@example.com",
            Password = "Str0ngPass1",
            ConfirmPassword = "Str0ngPass1",
            Role = "User",
            IsActive = true
        });

        AssertNoSecrets(await response.Content.ReadAsStringAsync());
    }
}
