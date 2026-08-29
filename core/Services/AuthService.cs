using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

public class AuthService
{
    private readonly OpenClientDbContext _db;

    private readonly ILogger<AuthService> _logger;

    public AuthService(
        OpenClientDbContext db,
        ILogger<AuthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User?> ValidateCredentialsAsync(LoginModel model)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == model.Email);

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
                ClaimTypes.Role,
                user.Role)
        };
    }
}