using Microsoft.EntityFrameworkCore;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Data.Repositories;

/// <inheritdoc cref="IClientRepository" />
public sealed class ClientRepository : IClientRepository
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;
    private readonly ILogger<ClientRepository> _logger;

    public ClientRepository(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        ILogger<ClientRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Client?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(client => client.Id == id && !client.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> GetPagedAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Clients
            .AsNoTracking()
            .Where(client => !client.IsDeleted);

        query = ApplySearch(query, filter.Search);
        query = ApplyIndustry(query, filter.Industry);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, filter.SortBy)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetRawIndustriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Clients
            .AsNoTracking()
            .Where(client => !client.IsDeleted
                && client.Industry != null
                && client.Industry != "")
            .Select(client => client.Industry!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(
        Client client,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cliente creado: Id={ClientId}.", client.Id);
        return client.Id;
    }

    public async Task<bool> UpdateAsync(
        int id,
        Action<Client> apply,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var client = await context.Clients
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.IsDeleted, cancellationToken);

        if (client is null)
        {
            _logger.LogWarning("Actualización ignorada: no existe el cliente Id={ClientId}.", id);
            return false;
        }

        apply(client);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cliente actualizado: Id={ClientId}.", id);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var client = await context.Clients
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.IsDeleted, cancellationToken);

        if (client is null)
        {
            _logger.LogWarning("Borrado ignorado: no existe el cliente Id={ClientId}.", id);
            return false;
        }

        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cliente eliminado (soft): Id={ClientId}.", id);
        return true;
    }

    public async Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Clients
            .AnyAsync(client => client.Id == id && !client.IsDeleted, cancellationToken);
    }

    // ---------- Composición de la consulta ----------

    private static IQueryable<Client> ApplySearch(IQueryable<Client> query, string? search)
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

    private static IQueryable<Client> ApplyIndustry(IQueryable<Client> query, string? industry)
    {
        if (string.IsNullOrWhiteSpace(industry))
        {
            return query;
        }

        var value = industry.Trim();

        return query.Where(client =>
            client.Industry != null && client.Industry.Trim() == value);
    }

    private static IQueryable<Client> ApplySort(IQueryable<Client> query, string? sortBy)
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
