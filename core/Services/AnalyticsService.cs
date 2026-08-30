using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenClient.Data;
using OpenClient.Interfaces;
using OpenClient.Models.Domain;
using OpenClient.Models.DTO.Analytics;

namespace OpenClient.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private const string UnknownLabel = "Unknown";

    private readonly IDbContextFactory<OpenClientDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly int _cacheSeconds;

    public AnalyticsService(
        IDbContextFactory<OpenClientDbContext> contextFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AnalyticsService> logger)
    {
        _contextFactory = contextFactory;
        _cache = cache;
        _logger = logger;
        _cacheSeconds = Math.Max(0, configuration.GetValue("Analytics:CacheSeconds", 0));
    }

    public async Task<DashboardAnalyticsDto> GetDashboardAsync(
        AnalyticsRange range,
        int top,
        GrowthBucket bucket,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"analytics:dash:{range.FromUtc:o}:{range.ToUtcExclusive:o}:{top}:{bucket}";

        if (_cacheSeconds > 0
            && _cache.TryGetValue(cacheKey, out DashboardAnalyticsDto? cached)
            && cached is not null)
        {
            return cached;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var scoped = Scoped(db, range);

        var dto = new DashboardAnalyticsDto
        {
            Period = new AnalyticsPeriodDto
            {
                From = range.FromLabel,
                To = range.ToLabel,
                Bucket = bucket.ToString().ToLowerInvariant()
            },
            Overview = await BuildOverviewAsync(db, scoped, range, cancellationToken),
            Completeness = await BuildCompletenessAsync(scoped, cancellationToken),
            Industries = await DistributeAsync(scoped, c => c.Industry, top, cancellationToken),
            Provinces = await DistributeAsync(scoped, c => c.Province, top, cancellationToken),
            Districts = await DistributeAsync(scoped, c => c.District, top, cancellationToken),
            JobTitles = await DistributeAsync(scoped, c => c.JobTitle, top, cancellationToken),
            Growth = await BuildGrowthAsync(scoped, range, bucket, cancellationToken)
        };

        if (_cacheSeconds > 0)
        {
            _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(_cacheSeconds));
        }

        return dto;
    }

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildOverviewAsync(db, Scoped(db, range), range, cancellationToken);
    }

    public async Task<CompletenessDto> GetCompletenessAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildCompletenessAsync(Scoped(db, range), cancellationToken);
    }

    public async Task<IReadOnlyList<DistributionDto>> GetIndustriesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await DistributeAsync(Scoped(db, range), c => c.Industry, top, cancellationToken);
    }

    public async Task<IReadOnlyList<DistributionDto>> GetProvincesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await DistributeAsync(Scoped(db, range), c => c.Province, top, cancellationToken);
    }

    public async Task<IReadOnlyList<DistributionDto>> GetDistrictsAsync(
        AnalyticsRange range,
        string? province,
        int top,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var scoped = Scoped(db, range);

        var normalizedProvince = province?.Trim();
        if (!string.IsNullOrEmpty(normalizedProvince))
        {
            scoped = scoped.Where(c => c.Province == normalizedProvince);
        }

        return await DistributeAsync(scoped, c => c.District, top, cancellationToken);
    }

    public async Task<IReadOnlyList<DistributionDto>> GetJobTitlesAsync(
        AnalyticsRange range,
        int top,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await DistributeAsync(Scoped(db, range), c => c.JobTitle, top, cancellationToken);
    }

    public async Task<ChartDataDto> GetGrowthAsync(
        AnalyticsRange range,
        GrowthBucket bucket,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var points = await BuildGrowthAsync(Scoped(db, range), range, bucket, cancellationToken);

        return new ChartDataDto
        {
            Bucket = bucket.ToString().ToLowerInvariant(),
            From = range.FromLabel,
            To = range.ToLabel,
            Points = points
        };
    }

    // ---------- Consulta base ----------

    private static IQueryable<Client> Scoped(OpenClientDbContext db, AnalyticsRange range) =>
        db.Clients
            .AsNoTracking()
            .Where(c => !c.IsDeleted
                && c.CreatedAt >= range.FromUtc
                && c.CreatedAt < range.ToUtcExclusive);

    // ---------- Overview ----------

    private static async Task<AnalyticsOverviewDto> BuildOverviewAsync(
        OpenClientDbContext db,
        IQueryable<Client> scoped,
        AnalyticsRange range,
        CancellationToken cancellationToken)
    {
        var totalClients = await db.Clients
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .LongCountAsync(cancellationToken);

        var coverage = await CoverageAsync(scoped, cancellationToken);

        var previousNew = await db.Clients
            .AsNoTracking()
            .Where(c => !c.IsDeleted
                && c.CreatedAt >= range.PreviousFromUtc
                && c.CreatedAt < range.FromUtc)
            .LongCountAsync(cancellationToken);

        return new AnalyticsOverviewDto
        {
            TotalClients = totalClients,
            NewClients = MetricDto.Compared(coverage.Total, previousNew),
            ClientsWithPhone = coverage.Phone,
            ClientsWithEmail = coverage.Email,
            ClientsWithWebsite = coverage.Website,
            ClientsWithAddress = coverage.Address,
            ClientsWithTaxId = coverage.TaxId
        };
    }

    // ---------- Completitud ----------

    private static async Task<CompletenessDto> BuildCompletenessAsync(
        IQueryable<Client> scoped,
        CancellationToken cancellationToken)
    {
        var c = await CoverageAsync(scoped, cancellationToken);

        return new CompletenessDto
        {
            TotalClients = c.Total,
            Phone = Item(c.Phone, c.Total),
            Email = Item(c.Email, c.Total),
            Website = Item(c.Website, c.Total),
            Address = Item(c.Address, c.Total),
            TaxId = Item(c.TaxId, c.Total)
        };

        static CompletenessItemDto Item(long count, long total) => new()
        {
            Count = count,
            Percentage = Percent(count, total)
        };
    }

    // Un único SELECT con SUM(CASE WHEN ...) para el total del período y la cobertura
    // de cada campo. Evita repetir seis consultas de conteo.
    private static async Task<CoverageRow> CoverageAsync(
        IQueryable<Client> scoped,
        CancellationToken cancellationToken)
    {
        var row = await scoped
            .GroupBy(_ => 1)
            .Select(g => new CoverageRow
            {
                Total = g.LongCount(),
                Phone = g.Sum(c => c.PhoneNumber != null && c.PhoneNumber.Trim() != "" ? 1L : 0L),
                Email = g.Sum(c => c.Email != null && c.Email.Trim() != "" ? 1L : 0L),
                Website = g.Sum(c => c.Website != null && c.Website.Trim() != "" ? 1L : 0L),
                Address = g.Sum(c => c.Address != null && c.Address.Trim() != "" ? 1L : 0L),
                TaxId = g.Sum(c => c.TaxId != null && c.TaxId.Trim() != "" ? 1L : 0L)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? new CoverageRow();
    }

    private sealed class CoverageRow
    {
        public long Total { get; init; }
        public long Phone { get; init; }
        public long Email { get; init; }
        public long Website { get; init; }
        public long Address { get; init; }
        public long TaxId { get; init; }
    }

    // ---------- Distribuciones ----------

    private static async Task<IReadOnlyList<DistributionDto>> DistributeAsync(
        IQueryable<Client> scoped,
        Expression<Func<Client, string?>> selector,
        int top,
        CancellationToken cancellationToken)
    {
        // GROUP BY en SQL: una fila por valor distinto de la columna.
        var raw = await scoped
            .GroupBy(selector)
            .Select(g => new { Key = g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);

        var total = raw.Sum(r => r.Count);

        return raw
            .GroupBy(
                r => string.IsNullOrWhiteSpace(r.Key) ? UnknownLabel : r.Key!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Label = group.Key.Equals(UnknownLabel, StringComparison.OrdinalIgnoreCase)
                    ? UnknownLabel
                    : group.OrderByDescending(x => x.Count).First().Key!.Trim(),
                Value = group.Sum(x => x.Count)
            })
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, top))
            .Select(x => new DistributionDto
            {
                Label = x.Label,
                Value = x.Value,
                Percentage = Percent(x.Value, total)
            })
            .ToList();
    }

    // ---------- Evolución temporal ----------

    private static async Task<IReadOnlyList<TimeSeriesDto>> BuildGrowthAsync(
        IQueryable<Client> scoped,
        AnalyticsRange range,
        GrowthBucket bucket,
        CancellationToken cancellationToken)
    {
        // Histograma diario calculado en SQL (GROUP BY año, mes, día). El nº de filas
        // está acotado por la longitud del rango, no por el nº de clientes.
        var daily = await scoped
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month, c.CreatedAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.LongCount() })
            .ToListAsync(cancellationToken);

        var counts = daily.ToDictionary(
            d => new DateOnly(d.Year, d.Month, d.Day),
            d => d.Count);

        var from = DateOnly.FromDateTime(range.FromUtc);
        var to = DateOnly.FromDateTime(range.ToUtcExclusive.AddDays(-1));

        var order = new List<string>();
        var acc = new Dictionary<string, long>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var label = BucketLabel(day, bucket);
            if (!acc.ContainsKey(label))
            {
                acc[label] = 0;
                order.Add(label);
            }

            acc[label] += counts.GetValueOrDefault(day);
        }

        return order
            .Select(label => new TimeSeriesDto { Period = label, Value = acc[label] })
            .ToList();
    }

    private static string BucketLabel(DateOnly day, GrowthBucket bucket) => bucket switch
    {
        GrowthBucket.Day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        GrowthBucket.Week => IsoWeekLabel(day),
        GrowthBucket.Year => day.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => day.ToString("yyyy-MM", CultureInfo.InvariantCulture)
    };

    private static string IsoWeekLabel(DateOnly day)
    {
        var dt = day.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(dt):D4}-W{ISOWeek.GetWeekOfYear(dt):D2}";
    }

    private static double Percent(long value, long total) =>
        total <= 0 ? 0 : Math.Round(value * 100.0 / total, 2);
}