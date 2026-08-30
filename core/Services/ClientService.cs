using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

/// <inheritdoc cref="IClientService" />
public sealed class ClientService : IClientService
{
    private readonly OpenClientDbContext _db;

    private readonly ILogger<ClientService> _logger;

    public ClientService(
        OpenClientDbContext db,
        ILogger<ClientService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<ClientListItemDto>> GetClientsAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Clients.AsNoTracking();

        query = ApplySearch(query, filter.Search);
        query = ApplyIndustry(query, filter.Industry);

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySort(query, filter.SortBy);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(client => new ClientListItemDto
            {
                Id = client.Id,
                CompanyName = client.CompanyName,
                LegalName = client.LegalName,
                FirstName = client.FirstName,
                LastName = client.LastName,
                JobTitle = client.JobTitle,
                Industry = client.Industry,
                TaxId = client.TaxId,
                Email = client.Email,
                PhoneNumber = client.PhoneNumber,
                Website = client.Website,
                Address = client.Address,
                District = client.District,
                Province = client.Province,
                CreatedAt = client.CreatedAt
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listado de clientes: página {Page} ({PageSize}/pág) · {Returned} de {Total} tras filtros.",
            filter.Page,
            filter.PageSize,
            items.Count,
            totalCount);

        return new PagedResult<ClientListItemDto>
        {
            Items = items,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<IReadOnlyList<string>> GetIndustriesAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await _db.Clients
            .AsNoTracking()
            .Where(client => client.Industry != null && client.Industry != "")
            .Select(client => client.Industry!)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Normaliza en memoria: descarta valores en blanco y colapsa
        // duplicados que solo difieren en espacios o mayúsculas.
        return raw
            .Select(industry => industry.Trim())
            .Where(industry => industry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(industry => industry, StringComparer.CurrentCulture)
            .ToList();
    }

    private static IQueryable<Client> ApplySearch(
        IQueryable<Client> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = $"%{search.Trim()}%";

        return query.Where(client =>
            (client.CompanyName != null && EF.Functions.Like(client.CompanyName, pattern)) ||
            (client.LegalName != null && EF.Functions.Like(client.LegalName, pattern)) ||
            (client.FirstName != null && EF.Functions.Like(client.FirstName, pattern)) ||
            (client.LastName != null && EF.Functions.Like(client.LastName, pattern)) ||
            (client.Email != null && EF.Functions.Like(client.Email, pattern)) ||
            (client.TaxId != null && EF.Functions.Like(client.TaxId, pattern)));
    }

    private static IQueryable<Client> ApplyIndustry(
        IQueryable<Client> query,
        string? industry)
    {
        if (string.IsNullOrWhiteSpace(industry))
        {
            return query;
        }

        // Compara sin espacios sobrantes: los valores de la lista vienen
        // normalizados pero la columna puede tenerlos.
        var value = industry.Trim();

        return query.Where(client =>
            client.Industry != null && client.Industry.Trim() == value);
    }

    private static IQueryable<Client> ApplySort(
        IQueryable<Client> query,
        string? sortBy)
    {
        return sortBy switch
        {
            "name" => query
                .OrderBy(client => client.CompanyName)
                .ThenBy(client => client.Id),
            "oldest" => query
                .OrderBy(client => client.CreatedAt)
                .ThenBy(client => client.Id),
            _ => query
                .OrderByDescending(client => client.CreatedAt)
                .ThenByDescending(client => client.Id)
        };
    }
}
