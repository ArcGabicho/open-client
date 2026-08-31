using OpenClient.Models.Domain;

namespace OpenClient.Models.DTO.Users;

// Detalle de un usuario. Incluye ConcurrencyStamp (testigo de concurrencia, no es
// un secreto) porque el formulario de edición lo devuelve. Nunca incluye
// PasswordHash, tokens ni security stamps.
public sealed class UserDetailDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public string ConcurrencyStamp { get; init; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public static UserDetailDto FromEntity(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        UserName = user.UserName,
        Email = user.Email,
        Roles = string.IsNullOrWhiteSpace(user.Role) ? [] : [user.Role],
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        ConcurrencyStamp = user.ConcurrencyStamp
    };
}