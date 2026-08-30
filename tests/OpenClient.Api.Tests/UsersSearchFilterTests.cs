using OpenClient.Api.Tests.Infrastructure;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;
using Xunit;

namespace OpenClient.Api.Tests;

public sealed class UsersSearchFilterTests : ApiTestBase
{
    public UsersSearchFilterTests(ApiFactory factory) : base(factory)
    {
    }

    private HttpClient Admin() => CreateAuthenticatedClient(roles: "Admin", userId: 1);

    private Task<PagedResult<UserListItemDto>> ListAsync(string query) =>
        GetJsonAsync<PagedResult<UserListItemDto>>(Admin(), "/api/users" + query);

    // El administrador que actúa se identifica por claims (cabeceras de prueba); no
    // necesita existir como fila. Así el orden de los resultados es determinista.
    private async Task SeedDirectoryAsync()
    {
        await SeedUsersAsync(
            UserFactory.Create(2, role: "Admin", isActive: true, firstName: "Alan", lastName: "Turing", userName: "aturing", email: "alan@bletchley.uk"),
            UserFactory.Create(3, role: "Manager", isActive: true, firstName: "Grace", lastName: "Hopper", userName: "ghopper", email: "grace@navy.mil"),
            UserFactory.Create(4, role: "User", isActive: false, firstName: "Edsger", lastName: "Dijkstra", userName: "ewd", email: "edsger@dijkstra.nl"),
            UserFactory.Create(5, role: "User", isActive: true, firstName: "Barbara", lastName: "Liskov", userName: "bliskov", email: "barbara@mit.edu"));
    }

    [Theory]
    [InlineData("?search=Turing", 1)]
    [InlineData("?search=grace", 1)]
    [InlineData("?search=ewd", 1)]
    [InlineData("?search=mit.edu", 1)]
    [InlineData("?search=zzz", 0)]
    public async Task Search_matches_name_username_and_email(string query, int expected)
    {
        await SeedDirectoryAsync();

        var page = await ListAsync(query);

        Assert.Equal(expected, page.TotalCount);
    }

    [Fact]
    public async Task Filter_by_status()
    {
        await SeedDirectoryAsync();

        Assert.Equal(3, (await ListAsync("?status=Active")).TotalCount);
        Assert.Equal(1, (await ListAsync("?status=Inactive")).TotalCount);
    }

    [Fact]
    public async Task Filter_by_role()
    {
        await SeedDirectoryAsync();

        Assert.Equal(1, (await ListAsync("?role=Admin")).TotalCount);
        Assert.Equal(1, (await ListAsync("?role=Manager")).TotalCount);
    }

    [Fact]
    public async Task Filters_combine()
    {
        await SeedDirectoryAsync();

        var page = await ListAsync("?status=Active&role=User");

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("bliskov", page.Items[0].UserName);
    }

    [Fact]
    public async Task Pagination_limits_and_reports_totals()
    {
        var users = new List<User> { UserFactory.Admin(1) };
        for (var i = 2; i <= 26; i++)
        {
            users.Add(UserFactory.Create(i));
        }

        await SeedUsersAsync(users.ToArray());

        var page = await ListAsync("?page=2&pageSize=10");

        Assert.Equal(26, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.Items.Count);
    }

    [Fact]
    public async Task PageSize_over_the_cap_falls_back_to_default()
    {
        await SeedDirectoryAsync();

        var page = await ListAsync("?pageSize=99999");

        Assert.Equal(UserSearchFilter.DefaultPageSize, page.PageSize);
    }

    [Fact]
    public async Task Sort_by_name_ascending_and_descending()
    {
        await SeedDirectoryAsync();

        var asc = await ListAsync("?sortBy=name&sortDir=asc&pageSize=100");
        var desc = await ListAsync("?sortBy=name&sortDir=desc&pageSize=100");

        Assert.Equal("Alan", asc.Items[0].FirstName);
        Assert.Equal("Grace", desc.Items[0].FirstName);
    }

    [Fact]
    public async Task Sort_by_email_ascending()
    {
        await SeedDirectoryAsync();

        var page = await ListAsync("?sortBy=email&sortDir=asc&pageSize=100");

        Assert.Equal("alan@bletchley.uk", page.Items[0].Email);
    }

    [Fact]
    public async Task Sort_by_created_descending_is_the_default()
    {
        await SeedDirectoryAsync();

        var page = await ListAsync("?pageSize=100");

        // CreatedAt = Base + id minutos → el id más alto es el más reciente.
        Assert.Equal(5, page.Items[0].Id);
    }
}
