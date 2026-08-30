using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpenClient.Data.Repositories;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Services;

/// <inheritdoc cref="IUserService" />
public sealed class UserService : IUserService
{
    private const int BcryptWorkFactor = 12;

    private readonly IUserRepository _repository;
    private readonly IUserAuditLogger _audit;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;
    private readonly IValidator<UserSearchFilter> _filterValidator;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository repository,
        IUserAuditLogger audit,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<ChangePasswordRequest> passwordValidator,
        IValidator<UserSearchFilter> filterValidator,
        ILogger<UserService> logger)
    {
        _repository = repository;
        _audit = audit;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _passwordValidator = passwordValidator;
        _filterValidator = filterValidator;
        _logger = logger;
    }

    // ---------- Lectura ----------

    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(
        ClaimsPrincipal actor,
        UserSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var validation = await _filterValidator.ValidateAsync(filter, cancellationToken);
        if (!validation.IsValid)
        {
            // Filtro corrupto: se normaliza a los valores por defecto.
            filter = new UserSearchFilter();
        }

        var (items, total) = await _repository.GetPagedAsync(filter, cancellationToken);

        return new PagedResult<UserListItemDto>
        {
            Items = items.Select(UserListItemDto.FromEntity).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total
        };
    }

    public async Task<UserDetailDto?> GetByIdAsync(
        ClaimsPrincipal actor,
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : UserDetailDto.FromEntity(user);
    }

    public IReadOnlyList<string> GetAvailableRoles() => UserRoles.All;

    // ---------- Alta ----------

    public async Task<UserMutationResult> CreateAsync(
        ClaimsPrincipal actor,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UserMutationResult.Invalid(validation.Errors.Select(e => e.ErrorMessage));
        }

        var email = request.Email.Trim();
        var userName = request.UserName.Trim();

        if (await _repository.EmailExistsAsync(email, null, cancellationToken))
        {
            return UserMutationResult.Conflict("duplicate_email", "Ya existe un usuario con ese email.");
        }

        if (await _repository.UserNameExistsAsync(userName, null, cancellationToken))
        {
            return UserMutationResult.Conflict("duplicate_username", "Ya existe un usuario con ese nombre de usuario.");
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserName = userName,
            Email = email,
            Role = request.Role,
            IsActive = request.IsActive,
            ProfileImage = string.Empty,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BcryptWorkFactor),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        try
        {
            var id = await _repository.AddAsync(user, cancellationToken);
            _audit.Record(UserAuditActions.Created, actor, id, $"role={user.Role}; active={user.IsActive}");
            return UserMutationResult.Ok(id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Alta de usuario rechazada por la base de datos (probable duplicado).");
            return UserMutationResult.Conflict("duplicate", "El email o el nombre de usuario ya están en uso.");
        }
    }

    // ---------- Edición ----------

    public async Task<UserMutationResult> UpdateAsync(
        ClaimsPrincipal actor,
        int id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UserMutationResult.Invalid(validation.Errors.Select(e => e.ErrorMessage));
        }

        var target = await _repository.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return UserMutationResult.NotFound();
        }

        var email = request.Email.Trim();
        var userName = request.UserName.Trim();

        if (await _repository.EmailExistsAsync(email, id, cancellationToken))
        {
            return UserMutationResult.Conflict("duplicate_email", "Ya existe otro usuario con ese email.");
        }

        if (await _repository.UserNameExistsAsync(userName, id, cancellationToken))
        {
            return UserMutationResult.Conflict("duplicate_username", "Ya existe otro usuario con ese nombre de usuario.");
        }

        var losesAdminPrivilege =
            target is { IsActive: true, Role: UserRoles.Admin }
            && (!request.IsActive || !string.Equals(request.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase));

        if (losesAdminPrivilege && await _repository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            return UserMutationResult.Forbidden(
                "last_admin", "No se puede dejar el sistema sin un administrador activo.");
        }

        if (IsActor(actor, id) && !request.IsActive)
        {
            return UserMutationResult.Forbidden(
                "forbidden_self_deactivate", "No puedes desactivar tu propia cuenta.");
        }

        var outcome = await _repository.MutateAsync(id, request.ConcurrencyStamp, user =>
        {
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.UserName = userName;
            user.Email = email;
            user.Role = request.Role;
            user.IsActive = request.IsActive;
        }, cancellationToken);

        return outcome switch
        {
            UserUpdateOutcome.NotFound => UserMutationResult.NotFound(),
            UserUpdateOutcome.ConcurrencyConflict => UserMutationResult.Conflict(
                "concurrency", "Otro administrador modificó este usuario. Vuelve a cargarlo e inténtalo de nuevo."),
            _ => Audited(UserAuditActions.Updated, actor, id)
        };
    }

    // ---------- Activar / desactivar ----------

    public async Task<UserMutationResult> SetActiveAsync(
        ClaimsPrincipal actor,
        int id,
        bool active,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var target = await _repository.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return UserMutationResult.NotFound();
        }

        if (target.IsActive == active)
        {
            return UserMutationResult.Ok(id); // idempotente
        }

        if (!active)
        {
            if (await WouldLeaveNoActiveAdminAsync(target, cancellationToken))
            {
                return UserMutationResult.Forbidden(
                    "last_admin", "No se puede desactivar al último administrador activo.");
            }

            if (IsActor(actor, id))
            {
                return UserMutationResult.Forbidden(
                    "forbidden_self_deactivate", "No puedes desactivar tu propia cuenta.");
            }
        }

        var outcome = await _repository.MutateAsync(id, null, u => u.IsActive = active, cancellationToken);
        if (outcome == UserUpdateOutcome.NotFound)
        {
            return UserMutationResult.NotFound();
        }

        return Audited(active ? UserAuditActions.Activated : UserAuditActions.Deactivated, actor, id);
    }

    // ---------- Roles ----------

    public async Task<UserMutationResult> AssignRoleAsync(
        ClaimsPrincipal actor,
        int id,
        string role,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        if (!UserRoles.IsKnown(role))
        {
            return UserMutationResult.Invalid([$"El rol '{role}' no existe o no está permitido."]);
        }

        var canonical = UserRoles.All.First(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        var target = await _repository.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return UserMutationResult.NotFound();
        }

        var demotesLastAdmin =
            target is { IsActive: true, Role: UserRoles.Admin }
            && !string.Equals(canonical, UserRoles.Admin, StringComparison.Ordinal)
            && await _repository.CountActiveAdminsAsync(cancellationToken) <= 1;

        if (demotesLastAdmin)
        {
            return UserMutationResult.Forbidden(
                "last_admin", "No se puede retirar el rol de administrador al último administrador activo.");
        }

        var outcome = await _repository.MutateAsync(id, null, u => u.Role = canonical, cancellationToken);
        if (outcome == UserUpdateOutcome.NotFound)
        {
            return UserMutationResult.NotFound();
        }

        return Audited(UserAuditActions.RoleAssigned, actor, id, $"role={canonical}");
    }

    public async Task<UserMutationResult> RemoveRoleAsync(
        ClaimsPrincipal actor,
        int id,
        string role,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var target = await _repository.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return UserMutationResult.NotFound();
        }

        if (!string.Equals(target.Role, role, StringComparison.OrdinalIgnoreCase))
        {
            return UserMutationResult.Ok(id); // el usuario no tenía ese rol
        }

        if (await WouldLeaveNoActiveAdminAsync(target, cancellationToken))
        {
            return UserMutationResult.Forbidden(
                "last_admin", "No se puede retirar el rol de administrador al último administrador activo.");
        }

        var outcome = await _repository.MutateAsync(id, null, u => u.Role = string.Empty, cancellationToken);
        if (outcome == UserUpdateOutcome.NotFound)
        {
            return UserMutationResult.NotFound();
        }

        return Audited(UserAuditActions.RoleRemoved, actor, id, $"role={role}");
    }

    // ---------- Contraseña ----------

    public async Task<UserMutationResult> ChangePasswordAsync(
        ClaimsPrincipal actor,
        int id,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var validation = await _passwordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UserMutationResult.Invalid(validation.Errors.Select(e => e.ErrorMessage));
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, BcryptWorkFactor);

        var outcome = await _repository.MutateAsync(id, null, u => u.PasswordHash = hash, cancellationToken);
        if (outcome == UserUpdateOutcome.NotFound)
        {
            return UserMutationResult.NotFound();
        }

        return Audited(UserAuditActions.PasswordChanged, actor, id);
    }

    // ---------- Eliminación ----------

    public async Task<UserMutationResult> DeleteAsync(
        ClaimsPrincipal actor,
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(actor);

        var target = await _repository.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return UserMutationResult.NotFound();
        }

        if (await WouldLeaveNoActiveAdminAsync(target, cancellationToken))
        {
            return UserMutationResult.Forbidden(
                "last_admin", "No se puede eliminar al último administrador activo. Desactívalo si es necesario.");
        }

        if (IsActor(actor, id))
        {
            return UserMutationResult.Forbidden(
                "forbidden_self_delete", "No puedes eliminar tu propia cuenta.");
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return UserMutationResult.NotFound();
        }

        return Audited(UserAuditActions.Deleted, actor, id);
    }

    // ---------- Helpers ----------

    private static void EnsureCanManage(ClaimsPrincipal actor)
    {
        if (actor.Identity?.IsAuthenticated != true || !actor.IsInRole(UserRoles.Admin))
        {
            throw new UsersAccessDeniedException();
        }
    }

    private static bool IsActor(ClaimsPrincipal actor, int userId)
    {
        var id = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var actorId) && actorId == userId;
    }

    private async Task<bool> WouldLeaveNoActiveAdminAsync(User target, CancellationToken cancellationToken)
    {
        if (!(target.IsActive && string.Equals(target.Role, UserRoles.Admin, StringComparison.Ordinal)))
        {
            return false;
        }

        return await _repository.CountActiveAdminsAsync(cancellationToken) <= 1;
    }

    private UserMutationResult Audited(string action, ClaimsPrincipal actor, int id, string? detail = null)
    {
        _audit.Record(action, actor, id, detail);
        return UserMutationResult.Ok(id);
    }
}
