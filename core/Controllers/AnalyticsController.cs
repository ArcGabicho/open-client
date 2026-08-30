using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Interfaces;
using OpenClient.Models.Api;
using OpenClient.Models.DTO.Analytics;

namespace OpenClient.Controllers;

// Módulo de Analíticas. Solo lectura, independiente del CRUD de Clientes. Usa la
// autorización existente (cualquier usuario autenticado); nunca es anónimo.
[ApiController]
[Authorize]
[Route("api/analytics")]
[Produces("application/json")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        var dto = await _analytics.GetDashboardAsync(
            range, query.ResolveTop(), query.ResolveBucket(), cancellationToken);

        return Ok(dto);
    }

    [HttpGet("industries")]
    [ProducesResponseType(typeof(IReadOnlyList<DistributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIndustries(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetIndustriesAsync(range, query.ResolveTop(), cancellationToken));
    }

    [HttpGet("provinces")]
    [ProducesResponseType(typeof(IReadOnlyList<DistributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProvinces(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetProvincesAsync(range, query.ResolveTop(), cancellationToken));
    }

    [HttpGet("districts")]
    [ProducesResponseType(typeof(IReadOnlyList<DistributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDistricts(
        [FromQuery] AnalyticsQuery query,
        [FromQuery] string? province,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetDistrictsAsync(
            range, province, query.ResolveTop(), cancellationToken));
    }

    [HttpGet("job-titles")]
    [ProducesResponseType(typeof(IReadOnlyList<DistributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetJobTitles(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetJobTitlesAsync(range, query.ResolveTop(), cancellationToken));
    }

    [HttpGet("growth")]
    [ProducesResponseType(typeof(ChartDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGrowth(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetGrowthAsync(range, query.ResolveBucket(), cancellationToken));
    }

    [HttpGet("completeness")]
    [ProducesResponseType(typeof(CompletenessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCompleteness(
        [FromQuery] AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryResolve(out var range, out var error))
        {
            return BadRequest(ApiErrorResponse.Create("invalid_period", error!));
        }

        return Ok(await _analytics.GetCompletenessAsync(range, cancellationToken));
    }
}
