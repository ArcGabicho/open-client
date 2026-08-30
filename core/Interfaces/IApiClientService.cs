using OpenClient.Models.Api;

namespace OpenClient.Interfaces;

/// <summary>
/// Lógica de lectura de la API de integración (<c>/api/v1</c>). Es independiente de
/// <see cref="IClientService"/> (panel administrativo): tiene su propio contrato,
/// sus propios DTOs y solo expone operaciones de solo lectura. Reutiliza el
/// <c>OpenClientDbContext</c> y las entidades EF Core.
/// </summary>
public interface IApiClientService
{
    /// <summary>
    /// Página de clientes ordenada por fecha de alta descendente. <paramref name="page"/>
    /// y <paramref name="pageSize"/> ya deben venir validados.
    /// </summary>
    Task<ApiPagedResponse<ApiClientDto>> GetClientsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cliente por identificador, o <c>null</c> si no existe o está borrado lógicamente.
    /// </summary>
    Task<ApiClientDto?> GetClientByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Búsqueda comercial con filtros combinables. <c>Page</c> y <c>PageSize</c> del
    /// <paramref name="request"/> ya deben venir validados.
    /// </summary>
    Task<ApiPagedResponse<ApiClientDto>> SearchClientsAsync(
        ApiClientSearchRequest request,
        CancellationToken cancellationToken = default);
}
