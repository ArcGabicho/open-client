using System.Net;
using System.Net.Http.Json;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersAuthTests : ApiTestBase
{
    public UsersAuthTests(ApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/1")]
    [InlineData("GET", "/api/users/roles")]
    [InlineData("POST", "/api/users")]
    [InlineData("DELETE", "/api/users/1")]
    public async Task Unauthenticated_is_rejected(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await CreateAnonymousClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("User")]
    [InlineData("Manager")]
    public async Task Non_admin_cannot_manage_users(string roles)
    {
        var response = await CreateAuthenticatedClient(roles: roles).GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_users()
    {
        await SeedUsersAsync(UserFactory.Admin(1), UserFactory.Create(2));

        var page = await GetJsonAsync<PagedResult<UserListItemDto>>(
            CreateAuthenticatedClient(roles: "Admin", userId: 1), "/api/users");

        Assert.Equal(2, page.TotalCount);
    }
}
