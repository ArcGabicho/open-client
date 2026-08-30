namespace OpenClient.Models.DTO;

/// <summary>
/// Datos editables de un cliente. Se usa tanto para crear ("Nuevo cliente")
/// como para actualizar ("Editar") desde el panel.
/// </summary>
public sealed class ClientEditModel
{
    public string? CompanyName { get; set; }

    public string? LegalName { get; set; }

    public string? Industry { get; set; }

    public string? TaxId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Website { get; set; }

    public string? Address { get; set; }

    public string? District { get; set; }

    public string? Province { get; set; }

    public static ClientEditModel FromDto(ClientListItemDto client) => new()
    {
        CompanyName = client.CompanyName,
        LegalName = client.LegalName,
        Industry = client.Industry,
        TaxId = client.TaxId,
        FirstName = client.FirstName,
        LastName = client.LastName,
        JobTitle = client.JobTitle,
        Email = client.Email,
        PhoneNumber = client.PhoneNumber,
        Website = client.Website,
        Address = client.Address,
        District = client.District,
        Province = client.Province
    };
}
