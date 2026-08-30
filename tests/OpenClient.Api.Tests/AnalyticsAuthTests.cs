using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsAuthTests : ApiTestBase
{
    public AnalyticsAuthTests(ApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/analytics")]
    [InlineData("/api/analytics/industries")]
    [InlineData("/api/analytics/provinces")]
    [InlineData("/api/analytics/districts")]
    [InlineData("/api/analytics/job-titles")]
    [InlineData("/api/analytics/growth")]
    [InlineData("/api/analytics/completeness")]
    public async Task Endpoints_require_authentication(string url)
    {
        var response = await CreateAnonymousClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Any_authenticated_user_is_allowed()
    {
        await SeedAsync(ClientFactory.CreateMany(3));

        // Sin rol: la página/endpoint solo exige [Authorize], no un rol concreto.
        var client = CreateAuthenticatedClient(roles: string.Empty);

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(client, "/api/analytics");

        Assert.Equal(3, dto.Overview.TotalClients);
    }
}
