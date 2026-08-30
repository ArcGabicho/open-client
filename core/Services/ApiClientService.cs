using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Interfaces;
using OpenClient.Models.Api;
using OpenClient.Models.Domain;

namespace OpenClient.Services.Api;

public sealed class ApiClientService : IApiClientService
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;
    private readonly ILogger<ApiClientService> _logger;

    public ApiClientService(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        ILogger<ApiClientService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ApiPagedResponse<ApiClientDto>> GetClientsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Client> query = context.Clients
            .AsNoTracking()
            .Where(client => !client.IsDeleted);

        return await PaginateAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<ApiClientDto?> GetClientByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Clients
            .AsNoTracking()
            .Where(client => client.Id == id && !client.IsDeleted)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApiPagedResponse<ApiClientDto>> SearchClientsAsync(
        ApiClientSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Client> query = context.Clients
            .AsNoTracking()
            .Where(client => !client.IsDeleted);

        var search = Normalize(request.Search);
        if (search is not null)
        {
            query = query.Where(client =>
                (client.CompanyName != null && client.CompanyName.Contains(search)) ||
                (client.LegalName != null && client.LegalName.Contains(search)) ||
                (client.FirstName != null && client.FirstName.Contains(search)) ||
                (client.LastName != null && client.LastName.Contains(search)) ||
                (client.Email != null && client.Email.Contains(search)) ||
                (client.TaxId != null && client.TaxId.Contains(search)));
        }

        var companyName = Normalize(request.CompanyName);
        if (companyName is not null)
        {
            query = query.Where(client =>
                client.CompanyName != null && client.CompanyName.Contains(companyName));
        }

        var legalName = Normalize(request.LegalName);
        if (legalName is not null)
        {
            query = query.Where(client =>
                client.LegalName != null && client.LegalName.Contains(legalName));
        }

        var industry = Normalize(request.Industry);
        if (industry is not null)
        {
            query = query.Where(client => client.Industry == industry);
        }

        var province = Normalize(request.Province);
        if (province is not null)
        {
            query = query.Where(client => client.Province == province);
        }

        var district = Normalize(request.District);
        if (district is not null)
        {
            query = query.Where(client => client.District == district);
        }

        var jobTitle = Normalize(request.JobTitle);
        if (jobTitle is not null)
        {
            query = query.Where(client => client.JobTitle == jobTitle);
        }

        var taxId = Normalize(request.TaxId);
        if (taxId is not null)
        {
            query = query.Where(client => client.TaxId == taxId);
        }

        return await PaginateAsync(query, request.Page, request.PageSize, cancellationToken);
    }

    // ---------- Paginación y proyección ----------

    private async Task<ApiPagedResponse<ApiClientDto>> PaginateAsync(
        IQueryable<Client> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // Orden lógico: filtros → CountAsync → OrderBy → Skip → Take → Select DTO → ToListAsync.
        var totalItems = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderByDescending(client => client.CreatedAt)
            .ThenByDescending(client => client.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "API v1: página {Page} ({PageSize}/pág) · {Returned} de {Total} tras filtros.",
            page, pageSize, data.Count, totalItems);

        return ApiPagedResponse<ApiClientDto>.From(data, page, pageSize, totalItems);
    }

    /// <summary>Proyección directa entidad → DTO, traducida a la lista de columnas en SQL.</summary>
    private static readonly System.Linq.Expressions.Expression<Func<Client, ApiClientDto>> ToDto =
        client => new ApiClientDto
        {
            Id = client.Id,
            CompanyName = client.CompanyName,
            LegalName = client.LegalName,
            Industry = client.Industry,
            FirstName = client.FirstName,
            LastName = client.LastName,
            JobTitle = client.JobTitle,
            TaxId = client.TaxId,
            PhoneNumber = client.PhoneNumber,
            Email = client.Email,
            Website = client.Website,
            Address = client.Address,
            District = client.District,
            Province = client.Province,
            CreatedAt = client.CreatedAt
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}