using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Data.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Client> Items, int TotalCount)> GetPagedAsync(
        ClientSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRawIndustriesAsync(
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(Client client, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        int id,
        Action<Client> apply,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}