using System.Globalization;

namespace OpenClient.Models.DTO.Analytics;

public enum GrowthBucket
{
    Day,
    Week,
    Month,
    Year
}

// Parámetros de consulta comunes a todos los endpoints de analíticas.
public sealed class AnalyticsQuery
{
    public const int DefaultTop = 10;
    public const int MaxTop = 50;
    public const int DefaultRangeDays = 365;

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? Top { get; set; }
    public string? Bucket { get; set; }

    public int ResolveTop() =>
        Top is null ? DefaultTop : Math.Clamp(Top.Value, 1, MaxTop);

    public GrowthBucket ResolveBucket() => Bucket?.Trim().ToLowerInvariant() switch
    {
        "day" => GrowthBucket.Day,
        "week" => GrowthBucket.Week,
        "year" => GrowthBucket.Year,
        _ => GrowthBucket.Month
    };

    // Resuelve el rango: aplica valores por defecto y valida from <= to.
    public bool TryResolve(out AnalyticsRange range, out string? error)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = To ?? today;
        var from = From ?? to.AddDays(-DefaultRangeDays);

        if (from > to)
        {
            range = default!;
            error = "'from' must be on or before 'to'.";
            return false;
        }

        range = AnalyticsRange.FromDates(from, to);
        error = null;
        return true;
    }
}

// Rango de fechas normalizado a UTC, con el período anterior equivalente para comparar.
public sealed record AnalyticsRange(DateTime FromUtc, DateTime ToUtcExclusive)
{
    public DateTime PreviousFromUtc => FromUtc - (ToUtcExclusive - FromUtc);

    public string FromLabel =>
        DateOnly.FromDateTime(FromUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string ToLabel =>
        DateOnly.FromDateTime(ToUtcExclusive.AddDays(-1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static AnalyticsRange FromDates(DateOnly from, DateOnly to) => new(
        from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
