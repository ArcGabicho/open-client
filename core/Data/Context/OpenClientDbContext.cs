using Microsoft.EntityFrameworkCore;
using OpenClient.Models.Domain;

namespace OpenClient.Data;

public class OpenClientDbContext : DbContext
{
    public OpenClientDbContext(DbContextOptions<OpenClientDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenClientDbContext).Assembly);
    }
}