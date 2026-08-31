namespace OpenClient.Models.DTO.Analytics;

// Valor agregado con comparación opcional contra el período anterior equivalente.
public sealed class MetricDto
{
    public long Value { get; init; }

    // null cuando no hay período previo con el que comparar (evita dividir por cero).
    public double? PercentageChange { get; init; }

    public static MetricDto Simple(long value) => new() { Value = value };

    public static MetricDto Compared(long value, long previous)
    {
        double? change = previous == 0
            ? null
            : Math.Round((value - previous) * 100.0 / previous, 2);

        return new MetricDto { Value = value, PercentageChange = change };
    }
}