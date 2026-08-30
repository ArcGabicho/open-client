using OpenClient.Models.Domain;

namespace OpenClient.Api.Tests.Infrastructure;

/// <summary>Construye entidades <see cref="Client"/> para sembrar en las pruebas.</summary>
public static class ClientFactory
{
    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static Client Create(
        int id,
        string? companyName = null,
        string? legalName = null,
        string? industry = null,
        string? province = null,
        string? district = null,
        string? jobTitle = null,
        string? taxId = null,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        bool isDeleted = false)
    {
        return new Client
        {
            Id = id,
            CompanyName = companyName ?? $"Company {id}",
            LegalName = legalName ?? $"Legal {id} S.A.",
            Industry = industry,
            Province = province,
            District = district,
            JobTitle = jobTitle,
            TaxId = taxId ?? $"TAX{id:D6}",
            FirstName = firstName ?? $"First{id}",
            LastName = lastName ?? $"Last{id}",
            Email = email ?? $"contact{id}@example.com",
            PhoneNumber = $"+51 900 000 {id:D3}",
            Website = $"https://company{id}.example.com",
            Address = $"{id} Test Street",
            CreatedAt = Base.AddMinutes(id),
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Base.AddDays(1) : null
        };
    }

    /// <summary>Cliente base (Id + valores por defecto) al que se aplica <paramref name="configure"/>.</summary>
    public static Client Create(int id, Action<Client> configure)
    {
        var client = Create(id);
        configure(client);
        return client;
    }

    /// <summary>Genera <paramref name="count"/> clientes con Id 1..count.</summary>
    public static Client[] CreateMany(int count)
    {
        var clients = new Client[count];
        for (var i = 0; i < count; i++)
        {
            clients[i] = Create(i + 1);
        }

        return clients;
    }
}
