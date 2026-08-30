namespace OpenClient.Api.Tests.Infrastructure;

/// <summary>Espejos locales del contrato JSON de la API, para deserializar en las pruebas.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, PaginationInfo Pagination);

public sealed record PaginationInfo(int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record ClientResource(
    int Id,
    string? CompanyName,
    string? LegalName,
    string? Industry,
    string? FirstName,
    string? LastName,
    string? JobTitle,
    string? TaxId,
    string? PhoneNumber,
    string? Email,
    string? Website,
    string? Address,
    string? District,
    string? Province,
    DateTime CreatedAt);

public sealed record ErrorResponse(ErrorDetail Error);

public sealed record ErrorDetail(string Code, string Message);
