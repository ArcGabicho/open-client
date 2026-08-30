using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;

    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        ILogger<AuthService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<User?> ValidateCredentialsAsync(
        LoginModel model,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == model.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "Intento de inicio de sesion con correo inexistente: {Email}",
                model.Email);

            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Intento de inicio de sesion de un usuario inactivo: UserId={UserId}",
                user.Id);

            return null;
        }

        bool passwordValid;

        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(
                model.Password,
                user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "El hash almacenado del usuario UserId={UserId} es invalido. Acceso rechazado.",
                user.Id);

            return null;
        }

        if (!passwordValid)
        {
            _logger.LogWarning(
                "Contrasena incorrecta para UserId={UserId}",
                user.Id);

            return null;
        }

        _logger.LogInformation(
            "Credenciales validas para UserId={UserId}",
            user.Id);

        return user;
    }

    public IEnumerable<Claim> CreateClaims(User user)
    {
        return new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.Email),

            new Claim(
                ClaimTypes.Email,
                user.Email),

            new Claim(
                ClaimTypes.GivenName,
                user.FirstName ?? string.Empty),

            new Claim(
                ClaimTypes.Surname,
                user.LastName ?? string.Empty),

            new Claim(
                ClaimTypes.Role,
                user.Role)
        };
    }

    public async Task RecordSuccessfulLoginAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                set => set.SetProperty(u => u.LastLoginAt, DateTime.UtcNow),
                cancellationToken);

        if (updated == 0)
        {
            _logger.LogWarning(
                "No se pudo sellar el último acceso: UserId={UserId} no encontrado.", userId);
        }
    }
}