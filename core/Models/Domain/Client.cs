namespace OpenClient.Models.Domain;

public class Client
{
    public int Id { get; set; }
    public string? CompanyName { get; set; }
    public string? LegalName { get; set; }
    public string? Industry { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public string? TaxId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public DateTime CreatedAt { get; set; }

    // Auditoría y borrado lógico
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}