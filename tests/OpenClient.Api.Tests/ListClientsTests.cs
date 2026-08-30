using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class ListClientsTests : ApiTestBase
{
    public ListClientsTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Empty_table_returns_empty_page_with_zero_totals()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients");
        var page = await ReadAsync<PagedResponse<ClientResource>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(page.Data);
        Assert.Equal(0, page.Pagination.TotalItems);
        Assert.Equal(0, page.Pagination.TotalPages);
        Assert.Equal(1, page.Pagination.Page);
        Assert.Equal(25, page.Pagination.PageSize);
    }

    [Fact]
    public async Task Uses_default_page_and_page_size()
    {
        await SeedAsync(ClientFactory.CreateMany(30));
        var client = CreateAuthenticatedClient();

        var page = await ReadAsync<PagedResponse<ClientResource>>(
            await client.GetAsync("/api/v1/clients"));

        Assert.Equal(1, page.Pagination.Page);
        Assert.Equal(25, page.Pagination.PageSize);
        Assert.Equal(30, page.Pagination.TotalItems);
        Assert.Equal(2, page.Pagination.TotalPages);
        Assert.Equal(25, page.Data.Count);
    }

    [Fact]
    public async Task Honours_page_and_page_size()
    {
        await SeedAsync(ClientFactory.CreateMany(30));
        var client = CreateAuthenticatedClient();

        var page = await ReadAsync<PagedResponse<ClientResource>>(
            await client.GetAsync("/api/v1/clients?page=3&pageSize=10"));

        Assert.Equal(3, page.Pagination.Page);
        Assert.Equal(10, page.Pagination.PageSize);
        Assert.Equal(30, page.Pagination.TotalItems);
        Assert.Equal(3, page.Pagination.TotalPages);
        Assert.Equal(10, page.Data.Count);
    }

    [Fact]
    public async Task Page_beyond_last_returns_empty_data_but_real_totals()
    {
        await SeedAsync(ClientFactory.CreateMany(5));
        var client = CreateAuthenticatedClient();

        var page = await ReadAsync<PagedResponse<ClientResource>>(
            await client.GetAsync("/api/v1/clients?page=99&pageSize=25"));

        Assert.Empty(page.Data);
        Assert.Equal(5, page.Pagination.TotalItems);
    }

    [Fact]
    public async Task Orders_by_creation_date_descending()
    {
        await SeedAsync(ClientFactory.CreateMany(5));
        var client = CreateAuthenticatedClient();

        var page = await ReadAsync<PagedResponse<ClientResource>>(
            await client.GetAsync("/api/v1/clients"));

        var ids = page.Data.Select(c => c.Id).ToArray();
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, ids);
    }

    [Fact]
    public async Task Excludes_soft_deleted_clients()
    {
        await SeedAsync(
            ClientFactory.Create(1),
            ClientFactory.Create(2, isDeleted: true),
            ClientFactory.Create(3));
        var client = CreateAuthenticatedClient();

        var page = await ReadAsync<PagedResponse<ClientResource>>(
            await client.GetAsync("/api/v1/clients"));

        Assert.Equal(2, page.Pagination.TotalItems);
        Assert.DoesNotContain(page.Data, c => c.Id == 2);
    }

    [Theory]
    [InlineData("/api/v1/clients?page=0")]
    [InlineData("/api/v1/clients?page=-1")]
    [InlineData("/api/v1/clients?pageSize=0")]
    [InlineData("/api/v1/clients?pageSize=101")]
    public async Task Invalid_pagination_returns_400_envelope(string url)
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(url);
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_pagination", body.Error.Code);
    }

    [Fact]
    public async Task Max_page_size_100_is_accepted()
    {
        await SeedAsync(ClientFactory.CreateMany(10));
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients?pageSize=100");
        var page = await ReadAsync<PagedResponse<ClientResource>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100, page.Pagination.PageSize);
    }
}
