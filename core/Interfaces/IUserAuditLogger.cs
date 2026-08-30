using System.Security.Claims;

namespace OpenClient.Interfaces;

// Auditoría de operaciones administrativas sobre usuarios. Reutiliza el logging
// existente (Serilog). Registra actor, acción, usuario objetivo y timestamp;
// nunca contraseñas, hashes ni tokens.
public interface IUserAuditLogger
{
    void Record(string action, ClaimsPrincipal actor, int targetUserId, string? detail = null);
}

public static class UserAuditActions
{
    public const string Created = "user.created";
    public const string Updated = "user.updated";
    public const string Activated = "user.activated";
    public const string Deactivated = "user.deactivated";
    public const string RoleAssigned = "user.role_assigned";
    public const string RoleRemoved = "user.role_removed";
    public const string Deleted = "user.deleted";
    public const string PasswordChanged = "user.password_changed";
}
