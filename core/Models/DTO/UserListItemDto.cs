using OpenClient.Models.Domain;

namespace OpenClient.Models.DTO.Users;

// Fila del listado de usuarios. Nunca incluye PasswordHash ni ningún secreto.
public sealed class UserListItemDto
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

    public string FullName => $"{FirstName} {LastName}".Trim();

    public static UserListItemDto FromEntity(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        UserName = user.UserName,
        Email = user.Email,
        Roles = string.IsNullOrWhiteSpace(user.Role) ? [] : [user.Role],
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
}