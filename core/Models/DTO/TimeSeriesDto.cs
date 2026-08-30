namespace OpenClient.Models.DTO.Analytics;

// Un punto de una serie temporal. Period es la etiqueta del bucket:
// "2026-01-15" (day), "2026-W03" (week), "2026-01" (month) o "2026" (year).
public sealed class TimeSeriesDto
{
    public string Period { get; init; } = string.Empty;
    public long Value { get; init; }
}
