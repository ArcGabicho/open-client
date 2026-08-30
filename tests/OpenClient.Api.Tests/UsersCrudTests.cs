using System.Net;
using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersCrudTests : ApiTestBase
{
    public UsersCrudTests(ApiFactory factory) : base(factory)
    {
    }

    private HttpClient Admin() => CreateAuthenticatedClient(roles: "Admin", userId: 1);

    private static CreateUserRequest NewUser(string suffix = "a") => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        UserName = $"ada.{suffix}",
        Email = $"ada.{suffix}@example.com",
        Password = "Str0ngPass1",
        ConfirmPassword = "Str0ngPass1",
        Role = "User",
        IsActive = true
    };

    [Fact]
    public async Task Create_returns_201_with_detail_and_no_secrets()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await Admin().PostAsJsonAsync("/api/users", NewUser());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordhash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"password\"", raw, StringComparison.OrdinalIgnoreCase);

        var detail = await ReadAsync<UserDetailDto>(response);
        Assert.Equal("ada.a", detail.UserName);
        Assert.Contains("User", detail.Roles);
        Assert.True(detail.IsActive);
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_and_username()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2, userName: "taken", email: "taken@example.com"));

        var dupEmail = NewUser("b");
        dupEmail.Email = "taken@example.com";
        var r1 = await Admin().PostAsJsonAsync("/api/users", dupEmail);
        Assert.Equal(HttpStatusCode.Conflict, r1.StatusCode);

        var dupName = NewUser("c");
        dupName.UserName = "taken";
        var r2 = await Admin().PostAsJsonAsync("/api/users", dupName);
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task Get_returns_detail_or_404()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(7, firstName: "Grace"));

        var found = await GetJsonAsync<UserDetailDto>(Admin(), "/api/users/7");
        Assert.Equal("Grace", found.FirstName);
        Assert.False(string.IsNullOrEmpty(found.ConcurrencyStamp));

        var missing = await Admin().GetAsync("/api/users/999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Update_changes_fields_and_bumps_concurrency_stamp()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(5, firstName: "Old", role: "User"));

        var before = await GetJsonAsync<UserDetailDto>(Admin(), "/api/users/5");

        var update = new UpdateUserRequest
        {
            FirstName = "New",
            LastName = before.LastName,
            UserName = before.UserName,
            Email = before.Email,
            Role = "Manager",
            IsActive = true,
            ConcurrencyStamp = before.ConcurrencyStamp
        };

        var response = await Admin().PutAsJsonAsync("/api/users/5", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await ReadAsync<UserDetailDto>(response);
        Assert.Equal("New", after.FirstName);
        Assert.Contains("Manager", after.Roles);
        Assert.NotEqual(before.ConcurrencyStamp, after.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_with_stale_concurrency_stamp_returns_409()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(5));

        var update = new UpdateUserRequest
        {
            FirstName = "X",
            LastName = "Y",
            UserName = "user5",
            Email = "user5@example.com",
            Role = "User",
            IsActive = true,
            ConcurrencyStamp = "stalevalue"
        };

        var response = await Admin().PutAsJsonAsync("/api/users/5", update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Activate_and_deactivate_toggle_status()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(3, isActive: true));

        var off = await Admin().PostAsync("/api/users/3/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.False((await ReloadUserAsync(3))!.IsActive);

        var on = await Admin().PostAsync("/api/users/3/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, on.StatusCode);
        Assert.True((await ReloadUserAsync(3))!.IsActive);
    }

    [Fact]
    public async Task Delete_removes_the_row_or_404()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(4));

        var ok = await Admin().DeleteAsync("/api/users/4");
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Null(await ReloadUserAsync(4));

        var again = await Admin().DeleteAsync("/api/users/4");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Roles_endpoint_lists_known_roles()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var roles = await GetJsonAsync<List<string>>(Admin(), "/api/users/roles");

        Assert.Contains("Admin", roles);
        Assert.Contains("Manager", roles);
        Assert.Contains("User", roles);
    }
}
