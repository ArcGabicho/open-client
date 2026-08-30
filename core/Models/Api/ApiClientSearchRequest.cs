namespace OpenClient.Models.Api;

/// <summary>
/// Parámetros de <c>GET /api/v1/clients/search</c>. Todos los filtros son opcionales
/// y combinables; el filtrado se traduce a SQL Server mediante EF Core (nunca en memoria).
/// </summary>
public sealed class ApiClientSearchRequest
{
    /// <summary>
    /// Texto libre. Coincide parcialmente contra nombre comercial, razón social,
    /// nombre y apellido del contacto, correo e identificación tributaria.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>Coincidencia parcial sobre el nombre comercial.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Coincidencia parcial sobre la razón social.</summary>
    public string? LegalName { get; set; }

    /// <summary>Industria exacta (sin distinguir mayúsculas).</summary>
    public string? Industry { get; set; }

    /// <summary>Provincia exacta (sin distinguir mayúsculas).</summary>
    public string? Province { get; set; }

    /// <summary>Distrito exacto (sin distinguir mayúsculas).</summary>
    public string? District { get; set; }

    /// <summary>Cargo exacto del contacto (sin distinguir mayúsculas).</summary>
    public string? JobTitle { get; set; }

    /// <summary>Identificación tributaria exacta.</summary>
    public string? TaxId { get; set; }

    /// <summary>Página solicitada (base 1). Por defecto <c>1</c>.</summary>
    public int Page { get; set; } = ApiPaging.DefaultPage;

    /// <summary>Tamaño de página. Por defecto <c>25</c>, máximo <c>100</c>.</summary>
    public int PageSize { get; set; } = ApiPaging.DefaultPageSize;
}
