using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenClient.Models.Domain;

namespace OpenClient.Data;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(client => client.Id);

        builder.Property(client => client.Id)
            .ValueGeneratedOnAdd();

        builder.Property(client => client.CompanyName)
            .HasMaxLength(100);

        builder.Property(client => client.LegalName)
            .HasMaxLength(100);

        builder.Property(client => client.Industry)
            .HasMaxLength(200);

        builder.Property(client => client.FirstName)
            .HasMaxLength(50);

        builder.Property(client => client.LastName)
            .HasMaxLength(50);

        builder.Property(client => client.JobTitle)
            .HasMaxLength(50);

        builder.Property(client => client.TaxId)
            .HasMaxLength(20);

        builder.Property(client => client.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(client => client.Email)
            .HasMaxLength(400);

        builder.Property(client => client.Website)
            .HasMaxLength(500);

        builder.Property(client => client.Address)
            .HasMaxLength(500);

        builder.Property(client => client.District)
            .HasMaxLength(100);

        builder.Property(client => client.Province)
            .HasMaxLength(100);

        builder.Property(client => client.CreatedAt)
            .IsRequired();

        builder.Property(client => client.UpdatedAt)
            .IsRequired(false);

        builder.Property(client => client.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(client => client.DeletedAt)
            .IsRequired(false);

        builder.HasIndex(client => client.IsDeleted);

        // Índices para las agregaciones del módulo de Analíticas: filtro temporal
        // sobre CreatedAt y GROUP BY sobre las dimensiones comerciales.
        builder.HasIndex(client => client.CreatedAt);
        builder.HasIndex(client => client.Industry);
        builder.HasIndex(client => client.Province);
        builder.HasIndex(client => client.District);
        builder.HasIndex(client => client.JobTitle);
    }
}