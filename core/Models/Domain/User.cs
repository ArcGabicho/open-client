namespace OpenClient.Models.Domain;

public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string ProfileImage { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    // Último inicio de sesión correcto; lo actualiza AuthController tras el sign-in.
    public DateTime? LastLoginAt { get; set; }

    // Testigo de concurrencia optimista (independiente del proveedor). Cambia en
    // cada edición; el formulario de edición lo devuelve para detectar escrituras
    // simultáneas.
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}