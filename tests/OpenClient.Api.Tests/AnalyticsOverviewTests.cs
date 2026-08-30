using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.DTO.Analytics;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class AnalyticsOverviewTests : ApiTestBase
{
    private const string YearRange = "?from=2026-01-01&to=2026-12-31";

    public AnalyticsOverviewTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Total_clients_counts_every_non_deleted_row()
    {
        await SeedAsync(
            ClientFactory.Create(1, c => c.CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            ClientFactory.Create(2, c => c.CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            ClientFactory.Create(3, c => { c.CreatedAt = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc); c.IsDeleted = true; }));

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + YearRange);

        Assert.Equal(2, dto.Overview.TotalClients);
        Assert.Equal(2, dto.Overview.NewClients.Value);
    }

    [Fact]
    public async Task Coverage_counts_only_populated_fields()
    {
        await SeedAsync(
            ClientFactory.Create(1, c =>
            {
                c.CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
                c.PhoneNumber = "+51 1 000";
                c.Email = "a@x.com";
                c.Website = "https://x.com";
                c.Address = "Street 1";
                c.TaxId = "20100000001";
            }),
            ClientFactory.Create(2, c =>
            {
                c.CreatedAt = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc);
                c.PhoneNumber = null;
                c.Email = "  ";       // en blanco => no cuenta
                c.Website = null;
                c.Address = null;
                c.TaxId = null;
            }),
            ClientFactory.Create(3, c =>
            {
                c.CreatedAt = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc);
                c.PhoneNumber = "+51 1 222";
                c.Email = null;
                c.Website = "https://y.com";
                c.Address = null;
                c.TaxId = "20100000003";
            }));

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + YearRange);
        var overview = dto.Overview;

        Assert.Equal(3, overview.TotalClients);
        Assert.Equal(2, overview.ClientsWithPhone);
        Assert.Equal(1, overview.ClientsWithEmail);
        Assert.Equal(2, overview.ClientsWithWebsite);
        Assert.Equal(1, overview.ClientsWithAddress);
        Assert.Equal(2, overview.ClientsWithTaxId);
    }

    [Fact]
    public async Task New_clients_only_counts_the_selected_period()
    {
        await SeedAsync(
            ClientFactory.Create(1, c => c.CreatedAt = new DateTime(2025, 12, 20, 0, 0, 0, DateTimeKind.Utc)),
            ClientFactory.Create(2, c => c.CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            ClientFactory.Create(3, c => c.CreatedAt = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc)),
            ClientFactory.Create(4, c => c.CreatedAt = new DateTime(2027, 1, 5, 0, 0, 0, DateTimeKind.Utc)));

        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + YearRange);

        Assert.Equal(4, dto.Overview.TotalClients);
        Assert.Equal(2, dto.Overview.NewClients.Value);
    }

    [Fact]
    public async Task Empty_system_returns_zeroed_overview_without_error()
    {
        var dto = await GetJsonAsync<DashboardAnalyticsDto>(CreateAuthenticatedClient(), "/api/analytics" + YearRange);

        Assert.Equal(0, dto.Overview.TotalClients);
        Assert.Equal(0, dto.Overview.NewClients.Value);
        Assert.Null(dto.Overview.NewClients.PercentageChange);
        Assert.Equal(0, dto.Overview.ClientsWithPhone);
    }
}
