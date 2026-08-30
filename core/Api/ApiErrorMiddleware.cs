using OpenClient.Models.Api;

namespace OpenClient.Api;

public sealed class ApiErrorMiddleware
{
    private const string Prefix = "/" + ApiV1.RoutePrefix;

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorMiddleware> _logger;

    public ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente abortó la petición; no hay error que servir.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en la API v1: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteError(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred while processing the request.");
            return;
        }

        if (context.Response.HasStarted || context.Response.ContentLength is > 0)
        {
            return;
        }

        var payload = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                ("unauthorized", "Authentication is required to access this resource."),
            StatusCodes.Status403Forbidden =>
                ("forbidden", "You do not have permission to access this resource."),
            StatusCodes.Status404NotFound =>
                ("not_found", "The requested resource was not found."),
            StatusCodes.Status405MethodNotAllowed =>
                ("method_not_allowed", "The HTTP method is not supported for this resource."),
            _ => default
        };

        if (payload != default)
        {
            await WriteError(context, context.Response.StatusCode, payload.Item1, payload.Item2);
        }
    }

    private static async Task WriteError(
        HttpContext context,
        int statusCode,
        string code,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Create(code, message));
    }
}

public static class ApiErrorMiddlewareExtensions
{
    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiErrorMiddleware>();
}