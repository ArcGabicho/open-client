namespace OpenClient.Models.DTO.Analytics;

// Respuesta del endpoint de evolución temporal: la serie más el bucket y el
// rango efectivamente aplicado (para que el dashboard no tenga que recalcularlos).
public sealed class ChartDataDto
{
    public string Bucket { get; init; } = "month";
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public IReadOnlyList<TimeSeriesDto> Points { get; init; } = [];
}
