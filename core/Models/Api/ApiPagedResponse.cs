namespace OpenClient.Models.Api;

/// <summary>
/// Envoltura estándar de las respuestas paginadas de la API de integración.
/// Forma del cuerpo JSON:
/// <code>
/// {
///   "data": [ ... ],
///   "pagination": { "page": 1, "pageSize": 25, "totalItems": 4040, "totalPages": 162 }
/// }
/// </code>
/// </summary>
/// <typeparam name="T">Tipo de cada elemento de <see cref="Data"/>.</typeparam>
public sealed class ApiPagedResponse<T>
{
    /// <summary>Elementos de la página actual.</summary>
    public IReadOnlyList<T> Data { get; init; } = [];

    /// <summary>Metadatos de paginación del universo filtrado.</summary>
    public ApiPaginationInfo Pagination { get; init; } = new();

    public static ApiPagedResponse<T> From(
        IReadOnlyList<T> data,
        int page,
        int pageSize,
        int totalItems) => new()
    {
        Data = data,
        Pagination = new ApiPaginationInfo
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = pageSize <= 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize)
        }
    };
}

/// <summary>Metadatos de paginación.</summary>
public sealed class ApiPaginationInfo
{
    /// <summary>Página solicitada (base 1).</summary>
    public int Page { get; init; }

    /// <summary>Tamaño de página aplicado.</summary>
    public int PageSize { get; init; }

    /// <summary>Total de elementos tras aplicar los filtros (no solo la página).</summary>
    public int TotalItems { get; init; }

    /// <summary>Total de páginas disponibles para el <see cref="PageSize"/> actual.</summary>
    public int TotalPages { get; init; }
}
