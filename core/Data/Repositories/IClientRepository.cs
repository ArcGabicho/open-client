using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Data.Repositories;

/// <summary>
/// Acceso a la tabla <c>Clients</c>. Cada operación abre su propio
/// <see cref="OpenClientDbContext"/> mediante
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/>, por
/// lo que es seguro usarla concurrentemente desde un circuito Blazor. Todas las
/// lecturas excluyen las filas con borrado lógico (<c>IsDeleted = true</c>).
/// </summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Página de resultados aplicando búsqueda, filtro de industria y orden.
    /// El filtrado y la paginación se traducen a SQL; <c>TotalCount</c> es el
    /// universo filtrado, no la página.
    /// </summary>
    Task<(IReadOnlyList<Client> Items, int TotalCount)> GetPagedAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Industrias distintas sin normalizar (tal cual están en la BD).</summary>
    Task<IReadOnlyList<string>> GetRawIndustriesAsync(
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga la entidad (rastreada), le aplica <paramref name="apply"/> y guarda.
    /// Devuelve <c>false</c> si el cliente no existe o ya está borrado.
    /// </summary>
    Task<bool> UpdateAsync(
        int id,
        Action<Client> apply,
        CancellationToken cancellationToken = default);

    /// <summary>Marca <c>IsDeleted = true</c> y fija <c>DeletedAt</c>.</summary>
    Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
