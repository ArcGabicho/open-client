using System.Net;
using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersSelfProtectionTests : ApiTestBase
{
    public UsersSelfProtectionTests(ApiFactory factory) : base(factory)
    {
    }

    // Actúa como el usuario cuyo Id se indica (cabecera X-Test-UserId).
    private HttpClient As(int userId) => CreateAuthenticatedClient(roles: "Admin", userId: userId);

    [Fact]
    public async Task Admin_cannot_deactivate_themselves_when_others_exist()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Admin(2));

        var response = await As(1).PostAsync("/api/users/1/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True((await ReloadUserAsync(1))!.IsActive);
    }

    [Fact]
    public async Task Admin_cannot_delete_themselves()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Admin(2));

        var response = await As(1).DeleteAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(await ReloadUserAsync(1));
    }

    [Fact]
    public async Task Deactivating_the_last_active_admin_is_blocked()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await As(1).PostAsync("/api/users/1/deactivate", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("last_admin", body);
        Assert.True((await ReloadUserAsync(1))!.IsActive);
    }

    [Fact]
    public async Task Deleting_the_last_active_admin_is_blocked()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await As(1).DeleteAsync("/api/users/1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("last_admin", body);
        Assert.NotNull(await ReloadUserAsync(1));
    }

    [Fact]
    public async Task Update_cannot_deactivate_the_last_active_admin()
    {
        await SeedUsersAsync(UserFactory.Admin(1));
        var detail = await GetJsonAsync<UserDetailDto>(As(1), "/api/users/1");

        var update = new UpdateUserRequest
        {
            FirstName = detail.FirstName,
            LastName = detail.LastName,
            UserName = detail.UserName,
            Email = detail.Email,
            Role = "Admin",
            IsActive = false,
            ConcurrencyStamp = detail.ConcurrencyStamp
        };

        var response = await As(1).PutAsJsonAsync("/api/users/1", update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True((await ReloadUserAsync(1))!.IsActive);
    }

    [Fact]
    public async Task Admin_can_deactivate_another_admin_when_one_remains()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Admin(2));

        var response = await As(1).PostAsync("/api/users/2/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await ReloadUserAsync(2))!.IsActive);
    }
}
