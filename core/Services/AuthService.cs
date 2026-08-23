using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenClient.Data;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Services;

public class AuthService
{
    private readonly OpenClientDbContext _db;

    public AuthService(OpenClientDbContext db)
    {
        _db = db;
    }

    public async Task<User?> ValidateCredentialsAsync(LoginModel model)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Email == model.Email &&
                x.IsActive);

        if (user is null)
            return null;

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            model.Password,
            user.PasswordHash);

        return passwordValid ? user : null;
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