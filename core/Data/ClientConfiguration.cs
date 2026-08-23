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
            .HasMaxLength(150);

        builder.Property(client => client.Industry)
            .HasMaxLength(200);

        builder.Property(client => client.FirstName)
            .HasMaxLength(100);

        builder.Property(client => client.LastName)
            .HasMaxLength(150);

        builder.Property(client => client.JobTitle)
            .HasMaxLength(100);

        builder.Property(client => client.TaxId)
            .HasMaxLength(20);

        builder.Property(client => client.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(client => client.Email)
            .HasMaxLength(320);

        builder.Property(client => client.Website)
            .HasMaxLength(500);

        builder.Property(client => client.Address)
            .HasMaxLength(300);

        builder.Property(client => client.District)
            .HasMaxLength(100);

        builder.Property(client => client.Province)
            .HasMaxLength(100);

        builder.Property(client => client.CreatedAt)
            .IsRequired();
    }
}