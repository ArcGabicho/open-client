namespace OpenClient.Models.Api;

public sealed class ApiClientSearchRequest
{
    public string? Search { get; set; }

    public string? CompanyName { get; set; }

    public string? LegalName { get; set; }

    public string? Industry { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? JobTitle { get; set; }

    public string? TaxId { get; set; }

    public int Page { get; set; } = ApiPaging.DefaultPage;

    public int PageSize { get; set; } = ApiPaging.DefaultPageSize;
}