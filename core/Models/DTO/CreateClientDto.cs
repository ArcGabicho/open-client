namespace OpenClient.Models.DTO;

public record CreateClientDto(
    string CompanyName,
    string? LegalName = null,
    string? TaxId = null,
    string? Industry = null,
    string? Email = null,
    string? PhoneNumber = null,
    string? Website = null,
    string? Address = null,
    string? District = null,
    string? Province = null,
    string? FirstName = null,
    string? LastName = null,
    string? JobTitle = null)
{
    public ClientEditModel ToEditModel() => new()
    {
        CompanyName = CompanyName,
        LegalName = LegalName,
        TaxId = TaxId,
        Industry = Industry,
        Email = Email,
        PhoneNumber = PhoneNumber,
        Website = Website,
        Address = Address,
        District = District,
        Province = Province,
        FirstName = FirstName,
        LastName = LastName,
        JobTitle = JobTitle
    };
}