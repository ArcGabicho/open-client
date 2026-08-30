using System.Net;
using System.Text.Json;
using OpenClient.Api.Tests.Infrastructure;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class OpenApiDocumentTests : ApiTestBase
{
    public OpenApiDocumentTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Serves_v1_document_with_the_three_read_endpoints()
    {
        var client = CreateAnonymousClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/clients", out _));
        Assert.True(paths.TryGetProperty("/api/v1/clients/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/clients/search", out _));
    }

    [Fact]
    public async Task Document_excludes_the_admin_crud_controller()
    {
        var client = CreateAnonymousClient();

        using var document = JsonDocument.Parse(
            await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        Assert.False(paths.TryGetProperty("/api/clients", out _));
    }
}
