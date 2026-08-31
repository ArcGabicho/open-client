namespace OpenClient.Models.DTO.Analytics;

// Rango efectivamente aplicado, tal como se devuelve al cliente.
public sealed class AnalyticsPeriodDto
{
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string Bucket { get; init; } = "month";
}

// Resumen completo para el dashboard de Analíticas (GET /api/analytics).
public sealed class DashboardAnalyticsDto
{
    public AnalyticsPeriodDto Period { get; init; } = new();
    public AnalyticsOverviewDto Overview { get; init; } = new();
    public CompletenessDto Completeness { get; init; } = new();
    public IReadOnlyList<DistributionDto> Industries { get; init; } = [];
    public IReadOnlyList<DistributionDto> Provinces { get; init; } = [];
    public IReadOnlyList<DistributionDto> Districts { get; init; } = [];
    public IReadOnlyList<DistributionDto> JobTitles { get; init; } = [];
    public IReadOnlyList<TimeSeriesDto> Growth { get; init; } = [];
}