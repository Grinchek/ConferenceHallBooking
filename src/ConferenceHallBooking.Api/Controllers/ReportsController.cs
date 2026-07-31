using ConferenceHallBooking.Application.DTOs.Reports;
using ConferenceHallBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHallBooking.Api.Controllers;

/// <summary>
/// Звіти та аналітика для бізнесу: виручка, завантаженість, популярні послуги.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    /// <summary>Зведена аналітика (dashboard).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AnalyticsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetAnalyticsAsync(from, to, cancellationToken));

    /// <summary>Виручка в розрізі залів.</summary>
    [HttpGet("revenue-by-hall")]
    [ProducesResponseType(typeof(IReadOnlyList<RevenueByHallDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RevenueByHallDto>>> GetRevenueByHall(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetRevenueByHallAsync(from, to, cancellationToken));

    /// <summary>Завантаженість залів (occupancy).</summary>
    [HttpGet("occupancy")]
    [ProducesResponseType(typeof(IReadOnlyList<OccupancyReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OccupancyReportDto>>> GetOccupancy(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetOccupancyAsync(from, to, cancellationToken));

    /// <summary>Найпопулярніші додаткові послуги.</summary>
    [HttpGet("popular-services")]
    [ProducesResponseType(typeof(IReadOnlyList<PopularServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PopularServiceDto>>> GetPopularServices(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetPopularServicesAsync(from, to, cancellationToken));
}
