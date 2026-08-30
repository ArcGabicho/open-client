namespace OpenClient.Models.Api;

/// <summary>
/// Formato de error consistente de la API de integración. Nunca expone stack traces,
/// excepciones internas, cadenas de conexión ni detalles de EF Core.
/// <code>
/// { "error": { "code": "client_not_found", "message": "The requested client was not found." } }
/// </code>
/// </summary>
public sealed class ApiErrorResponse
{
    public ApiErrorResponse()
    {
    }

    public ApiErrorResponse(string code, string message)
    {
        Error = new ApiErrorDetail { Code = code, Message = message };
    }

    /// <summary>Detalle del error.</summary>
    public ApiErrorDetail Error { get; init; } = new();

    public static ApiErrorResponse Create(string code, string message) => new(code, message);
}

/// <summary>Par código/mensaje de un error de la API.</summary>
public sealed class ApiErrorDetail
{
    /// <summary>Código estable, legible por máquina (p. ej. <c>client_not_found</c>).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Mensaje breve orientado a personas. No contiene información interna.</summary>
    public string Message { get; init; } = string.Empty;
}
