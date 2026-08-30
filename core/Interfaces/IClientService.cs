using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

/// <summary>
/// Acceso de solo lectura al catálogo de clientes. Toda la paginación,
/// el filtrado y el orden se resuelven en la base de datos (SQL Server).
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
}
