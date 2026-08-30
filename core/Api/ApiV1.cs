namespace OpenClient.Api;

public static class ApiV1
{
    public const string RoutePrefix = "api/v1";

    public const string ReadPolicy = "ApiV1.Read";

    public const string OpenApiDocumentName = "v1";

    public const string CorsPolicy = "ApiV1.Cors";

    public static readonly string[] AllowedRoles = ["Admin", "Integrations"];
}
