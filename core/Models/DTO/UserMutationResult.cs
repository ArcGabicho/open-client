namespace OpenClient.Models.DTO.Users;

public enum UserMutationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Conflict,      // email/username duplicado, o concurrencia
    Forbidden      // autoprotección, último administrador, rol no permitido
}

// Resultado de una operación de escritura sobre usuarios. Nunca transporta
// excepciones internas: solo un código estable y un mensaje apto para la UI.
public sealed class UserMutationResult
{
    public UserMutationStatus Status { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public int? UserId { get; init; }

    public bool Succeeded => Status == UserMutationStatus.Success;

    public static UserMutationResult Ok(int? userId = null) =>
        new() { Status = UserMutationStatus.Success, UserId = userId };

    public static UserMutationResult Invalid(IEnumerable<string> errors) => new()
    {
        Status = UserMutationStatus.ValidationFailed,
        Code = "validation_failed",
        Message = "Revisa los datos del formulario.",
        ValidationErrors = errors.ToList()
    };

    public static UserMutationResult NotFound() => new()
    {
        Status = UserMutationStatus.NotFound,
        Code = "user_not_found",
        Message = "El usuario no existe."
    };

    public static UserMutationResult Conflict(string code, string message) =>
        new() { Status = UserMutationStatus.Conflict, Code = code, Message = message };

    public static UserMutationResult Forbidden(string code, string message) =>
        new() { Status = UserMutationStatus.Forbidden, Code = code, Message = message };
}
