using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsIndustryTests : ApiTestBase
{
    private const string YearRange = "?from=2026-01-01&to=2026-12-31";

    public AnalyticsIndustryTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task SeedIndustriesAsync()
    {
        var clients = new List<OpenClient.Models.Domain.Client>();
        var id = 1;

        void Add(int count, string? industry)
        {
            for (var i = 0; i < count; i++)
            {
                var current = id++;
                clients.Add(ClientFactory.Create(current, c =>
                {
                    c.Industry = industry;
                    c.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(current);
                }));
            }
        }

        Add(5, "Technology");
        Add(3, "Retail");
        Add(2, "Finance");
        Add(4, null);   // sin industria -> Unknown

        await SeedAsync(clients.ToArray());
    }

    [Fact]
    public async Task Groups_and_orders_descending_by_count()
    {
        await SeedIndustriesAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/industries" + YearRange);

        Assert.Equal(
            new[] { "Technology", "Unknown", "Retail", "Finance" },
            result.Select(d => d.Label).ToArray());
        Assert.Equal(new long[] { 5, 4, 3, 2 }, result.Select(d => d.Value).ToArray());
    }

    [Fact]
    public async Task Null_industry_is_grouped_as_unknown()
    {
        await SeedIndustriesAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/industries" + YearRange);

        var unknown = Assert.Single(result, d => d.Label == "Unknown");
        Assert.Equal(4, unknown.Value);
    }

    [Fact]
    public async Task Percentages_sum_to_roughly_one_hundred()
    {
        await SeedIndustriesAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/industries" + YearRange);

        Assert.Equal(100.0, result.Sum(d => d.Percentage), precision: 1);
        Assert.All(result, d => Assert.True(d.Percentage > 0));
    }

    [Fact]
    public async Task Top_parameter_limits_the_number_of_categories()
    {
        await SeedIndustriesAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/industries" + YearRange + "&top=2");

        Assert.Equal(2, result.Count);
        Assert.Equal("Technology", result[0].Label);
        Assert.Equal("Unknown", result[1].Label);
    }

    [Fact]
    public async Task Blank_and_whitespace_industries_fold_into_unknown()
    {
        await SeedAsync(
            ClientFactory.Create(1, c => { c.Industry = "  "; c.CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc); }),
            ClientFactory.Create(2, c => { c.Industry = null; c.CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc); }),
            ClientFactory.Create(3, c => { c.Industry = "Tech"; c.CreatedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc); }));

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/industries" + YearRange);

        Assert.Equal(2, result.Count);
        Assert.Equal("Unknown", result[0].Label);
        Assert.Equal(2, result[0].Value);
    }
}
