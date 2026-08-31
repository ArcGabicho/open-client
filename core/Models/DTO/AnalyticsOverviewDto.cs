namespace OpenClient.Models.DTO.Analytics;

// Métricas generales del dashboard. TotalClients es global (toda la cartera viva);
// el resto está acotado al período seleccionado.
public sealed class AnalyticsOverviewDto
{
    public long TotalClients { get; init; }
    public MetricDto NewClients { get; init; } = MetricDto.Simple(0);
    public long ClientsWithPhone { get; init; }
    public long ClientsWithEmail { get; init; }
    public long ClientsWithWebsite { get; init; }
    public long ClientsWithAddress { get; init; }
    public long ClientsWithTaxId { get; init; }
}