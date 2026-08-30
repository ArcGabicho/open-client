namespace OpenClient.Models.Api;

/// <summary>
/// Contrato público de un cliente expuesto por la API de integración (<c>/api/v1</c>).
/// Es independiente de las entidades EF Core y de los DTOs del panel administrativo:
/// cualquier cambio aquí es un cambio del contrato REST.
/// </summary>
public sealed class ApiClientDto
{
    /// <summary>Identificador único del cliente.</summary>
    public int Id { get; init; }

    /// <summary>Nombre comercial.</summary>
    public string? CompanyName { get; init; }

    /// <summary>Razón social.</summary>
    public string? LegalName { get; init; }

    /// <summary>Industria o sector.</summary>
    public string? Industry { get; init; }

    /// <summary>Nombre del contacto principal.</summary>
    public string? FirstName { get; init; }

    /// <summary>Apellido del contacto principal.</summary>
    public string? LastName { get; init; }

    /// <summary>Cargo del contacto principal.</summary>
    public string? JobTitle { get; init; }

    /// <summary>Identificación tributaria (RUC/NIF/…).</summary>
    public string? TaxId { get; init; }

    /// <summary>Teléfono de contacto.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Correo electrónico de contacto.</summary>
    public string? Email { get; init; }

    /// <summary>Sitio web.</summary>
    public string? Website { get; init; }

    /// <summary>Dirección.</summary>
    public string? Address { get; init; }

    /// <summary>Distrito.</summary>
    public string? District { get; init; }

    /// <summary>Provincia.</summary>
    public string? Province { get; init; }

    /// <summary>Fecha de alta (UTC).</summary>
    public DateTime CreatedAt { get; init; }
}
