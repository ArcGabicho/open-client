using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsGrowthTests : ApiTestBase
{
    public AnalyticsGrowthTests(ApiFactory factory) : base(factory)
    {
    }

    private static Client OnDay(int id, int year, int month, int day) =>
        ClientFactory.Create(id, c => c.CreatedAt = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Groups_by_month_and_zero_fills_gaps()
    {
        await SeedAsync(
            OnDay(1, 2026, 1, 5), OnDay(2, 2026, 1, 20), OnDay(3, 2026, 1, 31),
            OnDay(4, 2026, 3, 2), OnDay(5, 2026, 3, 15));

        var chart = await GetJsonAsync<ChartDataDto>(
            CreateAuthenticatedClient(), "/api/analytics/growth?from=2026-01-01&to=2026-03-31");

        Assert.Equal("month", chart.Bucket);
        Assert.Equal(
            new[] { "2026-01", "2026-02", "2026-03" },
            chart.Points.Select(p => p.Period).ToArray());
        Assert.Equal(new long[] { 3, 0, 2 }, chart.Points.Select(p => p.Value).ToArray());
    }

    [Fact]
    public async Task Only_counts_rows_inside_the_range()
    {
        await SeedAsync(
            OnDay(1, 2025, 12, 30),
            OnDay(2, 2026, 2, 10),
            OnDay(3, 2026, 2, 11),
            OnDay(4, 2026, 5, 1));

        var chart = await GetJsonAsync<ChartDataDto>(
            CreateAuthenticatedClient(), "/api/analytics/growth?from=2026-02-01&to=2026-02-28");

        Assert.Equal(2, chart.Points.Sum(p => p.Value));
        Assert.Single(chart.Points);
        Assert.Equal("2026-02", chart.Points[0].Period);
    }

    [Fact]
    public async Task Empty_period_returns_zeroed_buckets()
    {
        await SeedAsync(OnDay(1, 2026, 1, 1));

        var chart = await GetJsonAsync<ChartDataDto>(
            CreateAuthenticatedClient(), "/api/analytics/growth?from=2026-06-01&to=2026-08-31");

        Assert.Equal(new[] { "2026-06", "2026-07", "2026-08" }, chart.Points.Select(p => p.Period).ToArray());
        Assert.All(chart.Points, p => Assert.Equal(0, p.Value));
    }

    [Fact]
    public async Task Supports_daily_bucket()
    {
        await SeedAsync(OnDay(1, 2026, 4, 1), OnDay(2, 2026, 4, 1), OnDay(3, 2026, 4, 3));

        var chart = await GetJsonAsync<ChartDataDto>(
            CreateAuthenticatedClient(), "/api/analytics/growth?from=2026-04-01&to=2026-04-03&bucket=day");

        Assert.Equal("day", chart.Bucket);
        Assert.Equal(new[] { "2026-04-01", "2026-04-02", "2026-04-03" }, chart.Points.Select(p => p.Period).ToArray());
        Assert.Equal(new long[] { 2, 0, 1 }, chart.Points.Select(p => p.Value).ToArray());
    }

    [Fact]
    public async Task Dashboard_growth_uses_month_bucket_by_default()
    {
        await SeedAsync(OnDay(1, 2026, 1, 10), OnDay(2, 2026, 2, 10));

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(
            CreateAuthenticatedClient(), "/api/analytics?from=2026-01-01&to=2026-02-28");

        Assert.Equal(new[] { "2026-01", "2026-02" }, dto.Growth.Select(p => p.Period).ToArray());
        Assert.Equal(new long[] { 1, 1 }, dto.Growth.Select(p => p.Value).ToArray());
    }
}
