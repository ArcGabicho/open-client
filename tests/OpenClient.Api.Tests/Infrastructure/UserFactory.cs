using OpenClient.Models.Domain;

namespace OpenClient.Api.Tests.Infrastructure;

public static class UserFactory
{
    // Contraseña conocida ya hasheada (BCrypt) para sembrar usuarios en pruebas.
    public const string KnownPassword = "Str0ngPass1";

    private static readonly string KnownHash =
        BCrypt.Net.BCrypt.HashPassword(KnownPassword, workFactor: 6);

    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static User Create(
        int id,
        string? role = "User",
        bool isActive = true,
        string? firstName = null,
        string? lastName = null,
        string? userName = null,
        string? email = null,
        DateTime? createdAt = null,
        DateTime? lastLoginAt = null)
    {
        return new User
        {
            Id = id,
            FirstName = firstName ?? $"First{id}",
            LastName = lastName ?? $"Last{id}",
            UserName = userName ?? $"user{id}",
            Email = email ?? $"user{id}@example.com",
            Role = role ?? string.Empty,
            IsActive = isActive,
            PasswordHash = KnownHash,
            ProfileImage = string.Empty,
            CreatedAt = createdAt ?? Base.AddMinutes(id),
            LastLoginAt = lastLoginAt,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }

    public static User Admin(int id, bool isActive = true) =>
        Create(id, role: "Admin", isActive: isActive, userName: $"admin{id}", email: $"admin{id}@example.com");
}
