using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenClient.Data;

public sealed class OpenClientDbContextFactory
    : IDesignTimeDbContextFactory<OpenClientDbContext>
{
    public OpenClientDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=OpenClientDb;User Id=sa;Password=placeholder;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<OpenClientDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OpenClientDbContext(options);
    }
}
