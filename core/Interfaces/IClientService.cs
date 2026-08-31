using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientListItemDto>> GetClientsAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<ClientListItemDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetIndustriesAsync(
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        ClientEditModel model,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        int id,
        ClientEditModel model,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}