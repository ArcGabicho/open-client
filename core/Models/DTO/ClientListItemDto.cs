using OpenClient.Models.Domain;

namespace OpenClient.Models.DTO;

public sealed class ClientListItemDto
{
    public int Id { get; init; }

    public string? CompanyName { get; init; }

    public string? LegalName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? JobTitle { get; init; }

    public string? Industry { get; init; }

    public string? TaxId { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Website { get; init; }

    public string? Address { get; init; }

    public string? District { get; init; }

    public string? Province { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static ClientListItemDto FromEntity(Client client) => new()
    {
        Id = client.Id,
        CompanyName = client.CompanyName,
        LegalName = client.LegalName,
        FirstName = client.FirstName,
        LastName = client.LastName,
        JobTitle = client.JobTitle,
        Industry = client.Industry,
        TaxId = client.TaxId,
        Email = client.Email,
        PhoneNumber = client.PhoneNumber,
        Website = client.Website,
        Address = client.Address,
        District = client.District,
        Province = client.Province,
        CreatedAt = client.CreatedAt,
        UpdatedAt = client.UpdatedAt
    };

    // ----- Cadenas derivadas para la UI -----

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CompanyName) ? CompanyName!
        : !string.IsNullOrWhiteSpace(LegalName) ? LegalName!
        : "—";

    public string ContactName =>
        string.Join(
            " ",
            new[] { FirstName, LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    public string Location =>
        string.Join(
            ", ",
            new[] { District, Province }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    public string Initials
    {
        get
        {
            var name = DisplayName;

            if (string.IsNullOrWhiteSpace(name) || name == "—")
            {
                return "—";
            }

            var words = name.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 1)
            {
                var word = words[0];
                return word[..Math.Min(2, word.Length)].ToUpperInvariant();
            }

            return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
        }
    }
}