using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsPeriodComparisonTests : ApiTestBase
{
    // Período actual: Q2 2026 (abr-jun). Período anterior equivalente: Q1 2026 (ene-mar).
    private const string CurrentQuarter = "?from=2026-04-01&to=2026-06-30";

    public AnalyticsPeriodComparisonTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task SeedAsync(int q1Count, int q2Count)
    {
        var clients = new List<Client>();
        var id = 1;

        for (var i = 0; i < q1Count; i++)
        {
            var current = id++;
            clients.Add(ClientFactory.Create(current, c =>
                c.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(current)));
        }

        for (var i = 0; i < q2Count; i++)
        {
            var current = id++;
            clients.Add(ClientFactory.Create(current, c =>
                c.CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)));
        }

        await base.SeedAsync(clients.ToArray());
    }

    [Fact]
    public async Task Positive_growth_reports_a_positive_percentage_change()
    {
        await SeedAsync(q1Count: 8, q2Count: 10);

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + CurrentQuarter);

        Assert.Equal(10, dto.Overview.NewClients.Value);
        Assert.Equal(25.0, dto.Overview.NewClients.PercentageChange);
    }

    [Fact]
    public async Task Negative_growth_reports_a_negative_percentage_change()
    {
        await SeedAsync(q1Count: 8, q2Count: 5);

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + CurrentQuarter);

        Assert.Equal(5, dto.Overview.NewClients.Value);
        Assert.Equal(-37.5, dto.Overview.NewClients.PercentageChange);
    }

    [Fact]
    public async Task Previous_period_of_zero_yields_null_percentage_change()
    {
        await SeedAsync(q1Count: 0, q2Count: 6);

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + CurrentQuarter);

        Assert.Equal(6, dto.Overview.NewClients.Value);
        Assert.Null(dto.Overview.NewClients.PercentageChange);
    }
}
