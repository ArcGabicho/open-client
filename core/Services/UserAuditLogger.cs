using System.Security.Claims;
using OpenClient.Interfaces;

namespace OpenClient.Services;

/// <inheritdoc cref="IUserAuditLogger" />
public sealed class UserAuditLogger : IUserAuditLogger
{
    private readonly ILogger<UserAuditLogger> _logger;

    public UserAuditLogger(ILogger<UserAuditLogger> logger)
    {
        _logger = logger;
    }

    public void Record(string action, ClaimsPrincipal actor, int targetUserId, string? detail = null)
    {
        _logger.LogInformation(
            "USER_AUDIT action={Action} actor={Actor} actorId={ActorId} targetUserId={TargetUserId} timestamp={Timestamp:o} detail={Detail}",
            action,
            actor.Identity?.Name ?? "unknown",
            actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            targetUserId,
            DateTime.UtcNow,
            detail ?? string.Empty);
    }
}
