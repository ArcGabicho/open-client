using System.Net;
using OpenClient.Api.Tests.Infrastructure;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class GetClientByIdTests : ApiTestBase
{
    public GetClientByIdTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Existing_client_returns_200_with_contract_shape()
    {
        await SeedAsync(ClientFactory.Create(
            42,
            companyName: "Contoso",
            industry: "Technology",
            province: "Lima",
            district: "Miraflores"));
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients/42");
        var body = await ReadAsync<ClientResource>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(42, body.Id);
        Assert.Equal("Contoso", body.CompanyName);
        Assert.Equal("Technology", body.Industry);
        Assert.Equal("Miraflores", body.District);
    }

    [Fact]
    public async Task Unknown_id_returns_404_envelope()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients/999999");
        var body = await ReadAsync<ErrorResponse>(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("client_not_found", body.Error.Code);
    }

    [Fact]
    public async Task Soft_deleted_client_returns_404()
    {
        await SeedAsync(ClientFactory.Create(7, isDeleted: true));
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients/7");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_integer_id_does_not_match_the_route()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/clients/not-a-number");

        // 'search' es la única ruta literal; cualquier otro segmento no numérico
        // no resuelve el parámetro {id:int}.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
