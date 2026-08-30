namespace OpenClient.Models.DTO;

/// <summary>
/// Filtros de búsqueda, orden y paginación para el listado de clientes.
/// Se enlaza desde la query string en el controlador y desde el estado
/// del componente Blazor.
/// </summary>
public sealed class ClientSearchFilter
{
    private int _page = 1;
    private int _pageSize = 10;

    /// <summary>Texto libre: razón social, nombre legal, contacto o correo.</summary>
    public string? Search { get; set; }

    /// <summary>Industria exacta por la que filtrar (opcional).</summary>
    public string? Industry { get; set; }

    /// <summary>Orden: <c>recent</c> (por defecto), <c>name</c> u <c>oldest</c>.</summary>
    public string SortBy { get; set; } = "recent";

    /// <summary>Página solicitada (1-indexada).</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Tamaño de página. Valores admitidos: 10, 25, 50, 100. Por defecto 10.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is 10 or 25 or 50 or 100 ? value : 10;
    }
}
