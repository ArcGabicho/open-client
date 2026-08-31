namespace OpenClient.Models.DTO.Users;

// Roles que reconoce el módulo de Usuarios. Se apoyan en el sistema de roles
// existente (columna User.Role + claim ClaimTypes.Role); "Admin" es el rol que ya
// aprovisiona el sistema. Toda asignación de rol se valida contra esta lista.
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    // Política de autorización de todo el módulo (backend).
    public const string AdminPolicy = "Users.Admin";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, User];

    // Roles cuya asignación/retiro exige confirmación y protecciones extra.
    public static readonly IReadOnlyList<string> Privileged = [Admin];

    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool IsPrivileged(string? role) =>
        role is not null && Privileged.Contains(role, StringComparer.OrdinalIgnoreCase);
}