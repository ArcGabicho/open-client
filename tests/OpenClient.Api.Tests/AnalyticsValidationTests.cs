using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsValidationTests : ApiTestBase
{
    public AnalyticsValidationTests(ApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/analytics?from=2026-06-01&to=2026-01-01")]
    [InlineData("/api/analytics/industries?from=2026-06-01&to=2026-01-01")]
    [InlineData("/api/analytics/growth?from=2026-12-31&to=2026-01-01")]
    public async Task From_after_to_returns_400(string url)
    {
        var response = await CreateAuthenticatedClient().GetAsync(url);
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_period", body.Error.Code);
    }

    [Fact]
    public async Task Missing_period_falls_back_to_a_default_range()
    {
        await SeedAsync(ClientFactory.Create(1, c => c.CreatedAt = DateTime.UtcNow.AddDays(-10)));

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics");

        Assert.False(string.IsNullOrWhiteSpace(dto.Period.From));
        Assert.False(string.IsNullOrWhiteSpace(dto.Period.To));
        Assert.Equal(1, dto.Overview.NewClients.Value);
    }

    [Fact]
    public async Task Top_is_clamped_to_the_maximum()
    {
        await SeedAsync(ClientFactory.CreateMany(5));

        var response = await CreateAuthenticatedClient()
            .GetAsync("/api/analytics/industries?from=2026-01-01&to=2026-12-31&top=9999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
