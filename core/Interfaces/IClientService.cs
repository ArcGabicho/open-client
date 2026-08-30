using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

/// <summary>
/// Caso de uso del catálogo de clientes. Orquesta el repositorio
/// (<see cref="OpenClient.Data.Repositories.IClientRepository"/>), la
/// validación con FluentValidation y el mapeo a DTO. No accede a EF Core
/// directamente.
/// </summary>
public interface IClientService
{
    /// <summary>Página del listado, con búsqueda, filtro de industria y orden.</summary>
    Task<PagedResult<ClientListItemDto>> GetClientsAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Un cliente por id, o <c>null</c> si no existe o está borrado.</summary>
    Task<ClientListItemDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Industrias distintas normalizadas (para el filtro y el autocompletado).</summary>
    Task<IReadOnlyList<string>> GetIndustriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Valida y crea un cliente. Devuelve su id.</summary>
    /// <exception cref="FluentValidation.ValidationException">Si el modelo no es válido.</exception>
    Task<int> CreateAsync(
        ClientEditModel model,
        CancellationToken cancellationToken = default);

    /// <summary>Valida y actualiza un cliente. Devuelve <c>false</c> si no existe.</summary>
    /// <exception cref="FluentValidation.ValidationException">Si el modelo no es válido.</exception>
    Task<bool> UpdateAsync(
        int id,
        ClientEditModel model,
        CancellationToken cancellationToken = default);

    /// <summary>Borrado lógico. Devuelve <c>false</c> si no existe o ya estaba borrado.</summary>
    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
