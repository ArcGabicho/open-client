using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class SearchClientsTests : ApiTestBase
{
    public SearchClientsTests(ApiFactory factory) : base(factory)
    {
    }

    private async Task SeedPortfolioAsync()
    {
        await SeedAsync(
            ClientFactory.Create(1,
                companyName: "Acme Technologies",
                legalName: "Acme Corp S.A.",
                industry: "Technology",
                province: "Lima",
                district: "Miraflores",
                jobTitle: "CTO",
                taxId: "20123456789"),
            ClientFactory.Create(2,
                companyName: "Beta Foods",
                legalName: "Beta Foods S.A.C.",
                industry: "Food",
                province: "Lima",
                district: "San Isidro",
                jobTitle: "CEO",
                taxId: "20987654321"),
            ClientFactory.Create(3,
                companyName: "Gamma Tech Partners",
                legalName: "Gamma SRL",
                industry: "Technology",
                province: "Arequipa",
                district: "Cercado",
                jobTitle: "CTO",
                taxId: "20555555555"));
    }

    private async Task<PagedResponse<ClientResource>> SearchAsync(string query)
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/v1/clients/search" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<PagedResponse<ClientResource>>(response);
    }

    [Fact]
    public async Task Free_text_search_matches_company_name()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?search=Acme");

        Assert.Single(page.Data);
        Assert.Equal(1, page.Data[0].Id);
    }

    [Fact]
    public async Task Free_text_search_matches_tax_id()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?search=20987654321");

        Assert.Single(page.Data);
        Assert.Equal(2, page.Data[0].Id);
    }

    [Fact]
    public async Task Filters_by_company_name_partially()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?companyName=Tech");

        Assert.Equal(new[] { 3, 1 }, page.Data.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Filters_by_legal_name_partially()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?legalName=Corp");

        Assert.Single(page.Data);
        Assert.Equal(1, page.Data[0].Id);
    }

    [Fact]
    public async Task Filters_by_industry_exactly()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?industry=Technology");

        Assert.Equal(2, page.Pagination.TotalItems);
        Assert.All(page.Data, c => Assert.Equal("Technology", c.Industry));
    }

    [Fact]
    public async Task Filters_by_province()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?province=Lima");

        Assert.Equal(2, page.Pagination.TotalItems);
    }

    [Fact]
    public async Task Filters_by_district()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?district=Miraflores");

        Assert.Single(page.Data);
        Assert.Equal(1, page.Data[0].Id);
    }

    [Fact]
    public async Task Filters_by_job_title()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?jobTitle=CTO");

        Assert.Equal(new[] { 3, 1 }, page.Data.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Filters_by_tax_id()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?taxId=20123456789");

        Assert.Single(page.Data);
        Assert.Equal(1, page.Data[0].Id);
    }

    [Fact]
    public async Task Combines_filters_with_and_semantics()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?industry=Technology&province=Lima&district=Miraflores");

        Assert.Single(page.Data);
        Assert.Equal(1, page.Data[0].Id);
    }

    [Fact]
    public async Task Combined_filters_with_no_match_return_empty()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync("?industry=Technology&province=Cusco");

        Assert.Empty(page.Data);
        Assert.Equal(0, page.Pagination.TotalItems);
    }

    [Fact]
    public async Task Search_supports_pagination()
    {
        await SeedAsync(ClientFactory.CreateMany(12));

        var page = await SearchAsync("?page=2&pageSize=5");

        Assert.Equal(2, page.Pagination.Page);
        Assert.Equal(5, page.Pagination.PageSize);
        Assert.Equal(12, page.Pagination.TotalItems);
        Assert.Equal(3, page.Pagination.TotalPages);
        Assert.Equal(5, page.Data.Count);
    }

    [Fact]
    public async Task Search_validates_pagination()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients/search?pageSize=500");
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_pagination", body.Error.Code);
    }

    [Fact]
    public async Task Search_without_filters_returns_all_non_deleted()
    {
        await SeedPortfolioAsync();

        var page = await SearchAsync(string.Empty);

        Assert.Equal(3, page.Pagination.TotalItems);
    }
}
