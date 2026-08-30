using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

/// <summary>
/// Acceso al catálogo de clientes. La consulta (paginación, filtrado y
/// orden) se resuelve en la base de datos (SQL Server); la creación y la
/// edición persisten mediante EF Core.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Devuelve una página del listado de clientes aplicando los filtros
    /// indicados. El conteo total corresponde al universo filtrado, no a
    /// la página.
    /// </summary>
    Task<PagedResult<ClientListItemDto>> GetClientsAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las industrias distintas presentes en la tabla de clientes,
    /// ordenadas alfabéticamente, para poblar el filtro correspondiente.
    /// </summary>
    Task<IReadOnlyList<string>> GetIndustriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea un cliente y devuelve su identificador.</summary>
    Task<int> CreateAsync(
        ClientEditModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los datos de un cliente existente. Devuelve <c>false</c>
    /// si el identificador no existe.
    /// </summary>
    Task<bool> UpdateAsync(
        int id,
        ClientEditModel model,
        CancellationToken cancellationToken = default);
}
