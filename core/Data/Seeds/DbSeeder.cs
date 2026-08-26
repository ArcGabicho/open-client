using Microsoft.EntityFrameworkCore;
using OpenClient.Data.SeedData;
using OpenClient.Models.Domain;

namespace OpenClient.Data;

public sealed class DbSeeder
{
    private readonly OpenClientDbContext _db;
    private readonly ILogger _logger;

    private const int BatchSize = 500;

    public DbSeeder(OpenClientDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedClientsAsync(CancellationToken ct = default)
    {
        var hasClients = await _db.Clients.AnyAsync(ct);

        if (hasClients)
        {
            _logger.LogInformation(
                "Seed de clientes omitido: la tabla ya contiene datos.");

            return;
        }

        var clients = ClientSeedData.Clients.ToList();

        if (clients.Count == 0)
        {
            _logger.LogWarning(
                "No se encontraron clientes en seed data.");

            return;
        }

        _logger.LogInformation(
            "Cargando {Count} clientes desde seed data...",
            clients.Count);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var inserted = 0;

            foreach (var batch in clients.Chunk(BatchSize))
            {
                _db.Clients.AddRange(batch);
                await _db.SaveChangesAsync(ct);

                inserted += batch.Length;

                _logger.LogInformation(
                    "Batch insertado: {Inserted}/{Total}",
                    inserted, clients.Count);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Seed completado: {Count} clientes insertados.",
                inserted);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);

            _logger.LogError(
                ex,
                "Error durante el seed de clientes. " +
                "Transaccion revertida.");

            throw;
        }
    }
}