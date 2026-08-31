namespace OpenClient.Models.Api;

public sealed class ApiErrorResponse
{
    public ApiErrorResponse()
    {
    }

    public ApiErrorResponse(string code, string message)
    {
        Error = new ApiErrorDetail { Code = code, Message = message };
    }

    public ApiErrorDetail Error { get; init; } = new();

    public static ApiErrorResponse Create(string code, string message) => new(code, message);
}

public sealed class ApiErrorDetail
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}