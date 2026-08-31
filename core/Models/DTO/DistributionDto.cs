namespace OpenClient.Models.DTO.Analytics;

// Una categoría dentro de una distribución (industria, provincia, distrito, cargo…).
public sealed class DistributionDto
{
    public string Label { get; init; } = string.Empty;
    public long Value { get; init; }
    public double Percentage { get; init; }
}