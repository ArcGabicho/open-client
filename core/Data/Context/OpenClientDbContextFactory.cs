using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenClient.Data;

/// <summary>
/// Fábrica en tiempo de diseño para las herramientas de EF Core
/// (`dotnet ef migrations`, `dotnet ef database update`). La aplicación en
/// ejecución NO la usa: en runtime se registra
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/>
/// vía DI. La cadena de conexión se toma de la variable de entorno
/// <c>ConnectionStrings__DefaultConnection</c>; para `migrations add` basta un
/// valor de marcador porque no se abre conexión.
/// </summary>
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
