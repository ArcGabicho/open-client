namespace OpenClient.Models.DTO;

/// <summary>Respuesta de <c>GET /api/clients/{id}</c>: el registro completo.</summary>
public record ClientDetailDto(
    int Id,
    string? CompanyName,
    string? LegalName,
    string? TaxId,
    string? Industry,
    string? Email,
    string? PhoneNumber,
    string? Website,
    string? Address,
    string? District,
    string? Province,
    string? FirstName,
    string? LastName,
    string? JobTitle,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ClientDetailDto FromListItem(ClientListItemDto client) => new(
        client.Id,
        client.CompanyName,
        client.LegalName,
        client.TaxId,
        client.Industry,
        client.Email,
        client.PhoneNumber,
        client.Website,
        client.Address,
        client.District,
        client.Province,
        client.FirstName,
        client.LastName,
        client.JobTitle,
        client.CreatedAt,
        client.UpdatedAt);
}
