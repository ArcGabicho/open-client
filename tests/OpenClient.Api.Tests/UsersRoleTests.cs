using System.Net;
using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersRoleTests : ApiTestBase
{
    public UsersRoleTests(ApiFactory factory) : base(factory)
    {
    }

    private HttpClient Admin() => CreateAuthenticatedClient(roles: "Admin", userId: 1);

    [Fact]
    public async Task Assign_role_updates_the_user()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2, role: "User"));

        var response = await Admin().PutAsJsonAsync("/api/users/2/role", new RoleRequest { Role = "Manager" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Manager", (await ReloadUserAsync(2))!.Role);
    }

    [Fact]
    public async Task Assign_privileged_role_is_allowed_for_an_admin()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2, role: "User"));

        var response = await Admin().PutAsJsonAsync("/api/users/2/role", new RoleRequest { Role = "Admin" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Admin", (await ReloadUserAsync(2))!.Role);
    }

    [Fact]
    public async Task Remove_role_clears_it()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2, role: "Manager"));

        var response = await Admin().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/2/role")
        {
            Content = JsonContent.Create(new RoleRequest { Role = "Manager" })
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, (await ReloadUserAsync(2))!.Role);
    }

    [Fact]
    public async Task Unknown_role_is_rejected_with_400()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var response = await Admin().PutAsJsonAsync("/api/users/2/role", new RoleRequest { Role = "Wizard" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_assign_roles()
    {
        await SeedUsersAsync(UserFactory.Create(2, role: "User"));

        var response = await CreateAuthenticatedClient(roles: "Manager")
            .PutAsJsonAsync("/api/users/2/role", new RoleRequest { Role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("User", (await ReloadUserAsync(2))!.Role);
    }

    [Fact]
    public async Task Removing_admin_from_the_last_active_admin_is_blocked()
    {
        // El actor (id 1) es el único administrador activo.
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await Admin().SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/1/role")
        {
            Content = JsonContent.Create(new RoleRequest { Role = "Admin" })
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Admin", (await ReloadUserAsync(1))!.Role);
    }
}
