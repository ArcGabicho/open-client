using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AuthenticationTests : ApiTestBase
{
    public AuthenticationTests(ApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/v1/clients")]
    [InlineData("/api/v1/clients/1")]
    [InlineData("/api/v1/clients/search")]
    public async Task Unauthenticated_request_returns_401(string url)
    {
        var client = CreateAnonymousClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_does_not_redirect_to_login_html()
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Clear();

        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_error_envelope()
    {
        var client = CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/clients");
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal("unauthorized", body.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.Message));
    }

    [Fact]
    public async Task Authenticated_without_allowed_role_returns_403()
    {
        var client = CreateAuthenticatedClient(roles: "Viewer");

        var response = await client.GetAsync("/api/v1/clients");
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", body.Error.Code);
    }

    [Fact]
    public async Task Authenticated_and_authorized_returns_200()
    {
        await SeedAsync(ClientFactory.CreateMany(3));
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
