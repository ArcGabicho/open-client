using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Data.Repositories;

// Acceso a la tabla Users. Cada operación abre su propio OpenClientDbContext vía
// IDbContextFactory (seguro frente a la concurrencia de un circuito Blazor).
// Búsqueda, filtros, orden y paginación se traducen a SQL.
public interface IUserRepository
{
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        UserSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);

    Task<bool> UserNameExistsAsync(string userName, int? excludeUserId = null, CancellationToken cancellationToken = default);

    // Nº de administradores activos (para no dejar el sistema sin administración).
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);

    Task<int> AddAsync(User user, CancellationToken cancellationToken = default);

    // Carga la entidad rastreada, aplica el cambio, renueva el ConcurrencyStamp y
    // guarda. Devuelve el resultado tipado (no encontrado / conflicto / ok).
    Task<UserUpdateOutcome> MutateAsync(
        int id,
        string? expectedConcurrencyStamp,
        Action<User> apply,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public enum UserUpdateOutcome
{
    Updated,
    NotFound,
    ConcurrencyConflict
}