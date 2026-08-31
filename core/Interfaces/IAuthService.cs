using System.Security.Claims;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

public interface IAuthService
{
    Task<User?> ValidateCredentialsAsync(
        LoginModel model,
        CancellationToken cancellationToken = default);

    IEnumerable<Claim> CreateClaims(User user);

    // Sella la fecha de último acceso tras un inicio de sesión correcto. Fuente
    // única: la columna Users.LastLoginAt.
    Task RecordSuccessfulLoginAsync(int userId, CancellationToken cancellationToken = default);
}