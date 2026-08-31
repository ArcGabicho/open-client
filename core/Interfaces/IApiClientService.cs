using OpenClient.Models.Api;

namespace OpenClient.Interfaces;

public interface IApiClientService
{
    Task<ApiPagedResponse<ApiClientDto>> GetClientsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ApiClientDto?> GetClientByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ApiPagedResponse<ApiClientDto>> SearchClientsAsync(
        ApiClientSearchRequest request,
        CancellationToken cancellationToken = default);
}