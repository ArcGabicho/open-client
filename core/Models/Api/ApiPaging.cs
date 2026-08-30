namespace OpenClient.Models.Api;

/// <summary>
/// Reglas de paginación compartidas por los endpoints de lista y de búsqueda
/// de la API de integración.
/// </summary>
public static class ApiPaging
{
    /// <summary>Página por defecto cuando el consumidor no envía <c>page</c>.</summary>
    public const int DefaultPage = 1;

    /// <summary>Tamaño de página por defecto cuando el consumidor no envía <c>pageSize</c>.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Tamaño de página máximo admitido.</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Valida <paramref name="page"/> y <paramref name="pageSize"/>. Devuelve <c>false</c>
    /// y un <see cref="ApiErrorResponse"/> con código <c>invalid_pagination</c> cuando
    /// <c>page &lt; 1</c> o <c>pageSize</c> está fuera de <c>[1, 100]</c>.
    /// </summary>
    public static bool TryValidate(
        int page,
        int pageSize,
        out ApiErrorResponse? error)
    {
        if (page < 1)
        {
            error = ApiErrorResponse.Create(
                "invalid_pagination",
                "The 'page' parameter must be greater than or equal to 1.");
            return false;
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            error = ApiErrorResponse.Create(
                "invalid_pagination",
                $"The 'pageSize' parameter must be between 1 and {MaxPageSize}.");
            return false;
        }

        error = null;
        return true;
    }
}
