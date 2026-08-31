using OpenClient.Models.DTO.Analytics;

namespace OpenClient.Interfaces;

// Capa analítica, independiente del CRUD de Clientes. Todas las agregaciones se
// ejecutan en SQL Server; nunca se materializa la tabla Clients completa en memoria.
// El rango acota las métricas del período; TotalClients es la única cifra global.
public interface IAnalyticsService
{
    Task<DashboardAnalyticsDto> GetDashboardAsync(
        AnalyticsRange range,
        int top,
        GrowthBucket bucket,
        CancellationToken cancellationToken = default);

    Task<AnalyticsOverviewDto> GetOverviewAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);

    Task<CompletenessDto> GetCompletenessAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionDto>> GetIndustriesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionDto>> GetProvincesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionDto>> GetDistrictsAsync(
        AnalyticsRange range,
        string? province,
        int top,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionDto>> GetJobTitlesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default);

    Task<ChartDataDto> GetGrowthAsync(
        AnalyticsRange range,
        GrowthBucket bucket,
        CancellationToken cancellationToken = default);
}