namespace OpenClient.Models.DTO.Analytics;

// Cobertura de un campo comercial: cuántos registros lo tienen y qué porcentaje.
public sealed class CompletenessItemDto
{
    public long Count { get; init; }
    public double Percentage { get; init; }
}

// Calidad/completitud de los datos comerciales dentro del período.
public sealed class CompletenessDto
{
    public long TotalClients { get; init; }
    public CompletenessItemDto Phone { get; init; } = new();
    public CompletenessItemDto Email { get; init; } = new();
    public CompletenessItemDto Website { get; init; } = new();
    public CompletenessItemDto Address { get; init; } = new();
    public CompletenessItemDto TaxId { get; init; } = new();
}