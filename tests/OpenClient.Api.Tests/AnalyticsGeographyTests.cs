using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsGeographyTests : ApiTestBase
{
    private const string YearRange = "?from=2026-01-01&to=2026-12-31";

    public AnalyticsGeographyTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task SeedGeographyAsync()
    {
        var seed = new (string? Province, string? District)[]
        {
            ("Lima", "Miraflores"),
            ("Lima", "Miraflores"),
            ("Lima", "San Isidro"),
            ("Lima", null),
            ("Arequipa", "Cercado"),
            ("Arequipa", "Cercado"),
            (null, null)
        };

        var clients = new List<Client>();
        for (var i = 0; i < seed.Length; i++)
        {
            var index = i + 1;
            clients.Add(ClientFactory.Create(index, c =>
            {
                c.Province = seed[i].Province;
                c.District = seed[i].District;
                c.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);
            }));
        }

        await SeedAsync(clients.ToArray());
    }

    [Fact]
    public async Task Provinces_are_ranked_by_count_desc_with_unknown_bucket()
    {
        await SeedGeographyAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/provinces" + YearRange);

        Assert.Equal(new[] { "Lima", "Arequipa", "Unknown" }, result.Select(d => d.Label).ToArray());
        Assert.Equal(new long[] { 4, 2, 1 }, result.Select(d => d.Value).ToArray());
    }

    [Fact]
    public async Task Districts_without_province_filter_cover_the_whole_portfolio()
    {
        await SeedGeographyAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/districts" + YearRange);

        Assert.Equal(2, result.Single(d => d.Label == "Miraflores").Value);
        Assert.Equal(2, result.Single(d => d.Label == "Cercado").Value);
        Assert.Equal(2, result.Single(d => d.Label == "Unknown").Value);
    }

    [Fact]
    public async Task Districts_can_be_filtered_by_province()
    {
        await SeedGeographyAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/districts" + YearRange + "&province=Lima");

        Assert.Equal(new[] { "Miraflores", "San Isidro", "Unknown" }, result.Select(d => d.Label).ToArray());
        Assert.Equal(new long[] { 2, 1, 1 }, result.Select(d => d.Value).ToArray());
        Assert.DoesNotContain(result, d => d.Label == "Cercado");
    }

    [Fact]
    public async Task Unknown_province_filter_returns_only_rows_without_province()
    {
        await SeedGeographyAsync();

        var result = await GetJsonAsync<List<DistributionDto>>(
            CreateAuthenticatedClient(), "/api/analytics/districts" + YearRange + "&province=Nowhere");

        Assert.Empty(result);
    }
}
