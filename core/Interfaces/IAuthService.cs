using System.Security.Claims;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO;

namespace OpenClient.Interfaces;

/// <summary>
/// Validación de credenciales y construcción de claims. El inicio y cierre de
/// sesión propiamente dichos (emisión de la cookie) los hace
/// <c>AuthController</c> sobre <c>HttpContext</c>.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Devuelve el usuario si el correo existe, está activo y la contraseña
    /// coincide con el hash BCrypt; en caso contrario, <c>null</c>.
    /// </summary>
    Task<User?> ValidateCredentialsAsync(
        LoginModel model,
        CancellationToken cancellationToken = default);

    /// <summary>Construye los claims de la identidad para la cookie.</summary>
    IEnumerable<Claim> CreateClaims(User user);
}
