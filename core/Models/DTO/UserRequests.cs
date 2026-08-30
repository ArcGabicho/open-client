namespace OpenClient.Models.DTO.Users;

// Alta de usuario por un administrador. La contraseña se procesa con el hasher
// existente (BCrypt); nunca se persiste en claro ni se devuelve.
public sealed class CreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
    public bool IsActive { get; set; } = true;
}

// Edición de campos administrables. No permite tocar campos internos (hash, stamps,
// EmailConfirmed, lockout…). ConcurrencyStamp: el valor leído en el detalle.
public sealed class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

// Cambio/reset administrativo de contraseña. Sin contraseña actual (lo hace un admin).
public sealed class ChangePasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

// Asignar / retirar rol.
public sealed class RoleRequest
{
    public string Role { get; set; } = string.Empty;
}
