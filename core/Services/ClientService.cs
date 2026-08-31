using FluentValidation;
using OpenClient.Data.Repositories;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;
    private readonly IValidator<ClientEditModel> _validator;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        IClientRepository repository,
        IValidator<ClientEditModel> validator,
        ILogger<ClientService> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<PagedResult<ClientListItemDto>> GetClientsAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(filter, cancellationToken);

        _logger.LogInformation(
            "Listado de clientes: página {Page} ({PageSize}/pág) · {Returned} de {Total} tras filtros.",
            filter.Page, filter.PageSize, items.Count, totalCount);

        return new PagedResult<ClientListItemDto>
        {
            Items = items.Select(ClientListItemDto.FromEntity).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ClientListItemDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var client = await _repository.GetByIdAsync(id, cancellationToken);
        return client is null ? null : ClientListItemDto.FromEntity(client);
    }

    public async Task<IReadOnlyList<string>> GetIndustriesAsync(
        CancellationToken cancellationToken = default)
    {
        var raw = await _repository.GetRawIndustriesAsync(cancellationToken);

        // Normaliza en memoria: recorta, descarta vacíos y colapsa duplicados
        // que solo difieren en espacios o mayúsculas.
        return raw
            .Select(industry => industry.Trim())
            .Where(industry => industry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(industry => industry, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<int> CreateAsync(
        ClientEditModel model,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(model, cancellationToken);

        var client = new Client { CreatedAt = DateTime.UtcNow };
        Apply(model, client);

        var id = await _repository.AddAsync(client, cancellationToken);
        _logger.LogInformation("Cliente creado desde el panel: Id={ClientId}.", id);
        return id;
    }

    public async Task<bool> UpdateAsync(
        int id,
        ClientEditModel model,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(model, cancellationToken);

        var updated = await _repository.UpdateAsync(id, client =>
        {
            Apply(model, client);
            client.UpdatedAt = DateTime.UtcNow;
        }, cancellationToken);

        if (!updated)
        {
            _logger.LogWarning("Actualización sin efecto: cliente Id={ClientId} inexistente.", id);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.SoftDeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            _logger.LogWarning("Borrado sin efecto: cliente Id={ClientId} inexistente.", id);
        }

        return deleted;
    }

    private static void Apply(ClientEditModel model, Client client)
    {
        client.CompanyName = Clean(model.CompanyName);
        client.LegalName = Clean(model.LegalName);
        client.Industry = Clean(model.Industry);
        client.TaxId = Clean(model.TaxId);
        client.FirstName = Clean(model.FirstName);
        client.LastName = Clean(model.LastName);
        client.JobTitle = Clean(model.JobTitle);
        client.Email = Clean(model.Email);
        client.PhoneNumber = Clean(model.PhoneNumber);
        client.Website = Clean(model.Website);
        client.Address = Clean(model.Address);
        client.District = Clean(model.District);
        client.Province = Clean(model.Province);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}