using System.Net;
using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersPasswordTests : ApiTestBase
{
    public UsersPasswordTests(ApiFactory factory) : base(factory)
    {
    }

    private HttpClient Admin() => CreateAuthenticatedClient(roles: "Admin", userId: 1);

    [Fact]
    public async Task Weak_password_is_rejected()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var response = await Admin().PostAsJsonAsync("/api/users/2/password",
            new ChangePasswordRequest { NewPassword = "short", ConfirmPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_without_a_digit_is_rejected()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var response = await Admin().PostAsJsonAsync("/api/users/2/password",
            new ChangePasswordRequest { NewPassword = "onlyletters", ConfirmPassword = "onlyletters" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirmation_mismatch_is_rejected()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var response = await Admin().PostAsJsonAsync("/api/users/2/password",
            new ChangePasswordRequest { NewPassword = "Str0ngPass1", ConfirmPassword = "Str0ngPass2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_password_change_succeeds_and_rehashes()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));
        var before = (await ReloadUserAsync(2))!.PasswordHash;

        var response = await Admin().PostAsJsonAsync("/api/users/2/password",
            new ChangePasswordRequest { NewPassword = "BrandNew9", ConfirmPassword = "BrandNew9" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = (await ReloadUserAsync(2))!.PasswordHash;
        Assert.NotEqual(before, after);
        Assert.True(BCrypt.Net.BCrypt.Verify("BrandNew9", after));
    }

    [Fact]
    public async Task Password_change_on_a_missing_user_returns_404()
    {
        await SeedUsersAsync(UserFactory.Admin(1));

        var response = await Admin().PostAsJsonAsync("/api/users/999/password",
            new ChangePasswordRequest { NewPassword = "BrandNew9", ConfirmPassword = "BrandNew9" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
