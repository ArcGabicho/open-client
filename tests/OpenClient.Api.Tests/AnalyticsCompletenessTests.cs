using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsCompletenessTests : ApiTestBase
{
    private const string YearRange = "?from=2026-01-01&to=2026-12-31";

    public AnalyticsCompletenessTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Percentages_are_computed_against_the_period_total()
    {
        // 4 clientes: 3 con teléfono, 1 con email.
        await SeedAsync(
            ClientFactory.Create(1, c => { c.CreatedAt = D(1); c.PhoneNumber = "1"; c.Email = "a@x.com"; }),
            ClientFactory.Create(2, c => { c.CreatedAt = D(2); c.PhoneNumber = "2"; c.Email = null; }),
            ClientFactory.Create(3, c => { c.CreatedAt = D(3); c.PhoneNumber = "3"; c.Email = null; }),
            ClientFactory.Create(4, c => { c.CreatedAt = D(4); c.PhoneNumber = null; c.Email = null; }));

        var dto = await GetJsonAsync<CompletenessDto>(
            CreateAuthenticatedClient(), "/api/analytics/completeness" + YearRange);

        Assert.Equal(4, dto.TotalClients);
        Assert.Equal(3, dto.Phone.Count);
        Assert.Equal(75.0, dto.Phone.Percentage);
        Assert.Equal(1, dto.Email.Count);
        Assert.Equal(25.0, dto.Email.Percentage);
    }

    [Fact]
    public async Task No_clients_in_period_yields_zero_percentages_without_dividing_by_zero()
    {
        await SeedAsync(ClientFactory.Create(1, c => c.CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var dto = await GetJsonAsync<CompletenessDto>(
            CreateAuthenticatedClient(), "/api/analytics/completeness" + YearRange);

        Assert.Equal(0, dto.TotalClients);
        Assert.Equal(0, dto.Phone.Count);
        Assert.Equal(0.0, dto.Phone.Percentage);
        Assert.Equal(0.0, dto.Email.Percentage);
        Assert.Equal(0.0, dto.Website.Percentage);
        Assert.Equal(0.0, dto.Address.Percentage);
        Assert.Equal(0.0, dto.TaxId.Percentage);
    }

    [Fact]
    public async Task Rounds_to_two_decimals()
    {
        // 3 clientes, 1 con website => 33.33 %
        await SeedAsync(
            ClientFactory.Create(1, c => { c.CreatedAt = D(1); c.Website = "https://x.com"; }),
            ClientFactory.Create(2, c => { c.CreatedAt = D(2); c.Website = null; }),
            ClientFactory.Create(3, c => { c.CreatedAt = D(3); c.Website = "   "; }));

        var dto = await GetJsonAsync<CompletenessDto>(
            CreateAuthenticatedClient(), "/api/analytics/completeness" + YearRange);

        Assert.Equal(1, dto.Website.Count);
        Assert.Equal(33.33, dto.Website.Percentage);
    }

    private static DateTime D(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);
}
