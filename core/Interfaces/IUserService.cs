using System.Security.Claims;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Interfaces;

// Orquestación del módulo de Usuarios. Cada método comprueba que el actor tenga
// permisos administrativos (chokepoint de autorización en backend), valida la
// entrada, aplica las protecciones (autoprotección, último administrador, rol
// permitido) y audita. Lanza UsersAccessDeniedException si el actor no puede
// gestionar usuarios.
public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(
        ClaimsPrincipal actor,
        UserSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto?> GetByIdAsync(
        ClaimsPrincipal actor,
        int id,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetAvailableRoles();

    Task<UserMutationResult> CreateAsync(
        ClaimsPrincipal actor,
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> UpdateAsync(
        ClaimsPrincipal actor,
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> SetActiveAsync(
        ClaimsPrincipal actor,
        int id,
        bool active,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> AssignRoleAsync(
        ClaimsPrincipal actor,
        int id,
        string role,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> RemoveRoleAsync(
        ClaimsPrincipal actor,
        int id,
        string role,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> ChangePasswordAsync(
        ClaimsPrincipal actor,
        int id,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<UserMutationResult> DeleteAsync(
        ClaimsPrincipal actor,
        int id,
        CancellationToken cancellationToken = default);
}

public sealed class UsersAccessDeniedException : Exception
{
    public UsersAccessDeniedException()
        : base("El usuario no tiene permisos para gestionar usuarios.")
    {
    }
}
