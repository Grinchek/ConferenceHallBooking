using ConferenceHallBooking.Application.DTOs.Common;
using ConferenceHallBooking.Application.DTOs.Halls;
using ConferenceHallBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHallBooking.Api.Controllers;

/// <summary>
/// Керування конференц-залами: створення, оновлення, видалення та пошук доступних.
/// </summary>
[ApiController]
[Route("api/v1/halls")]
[Produces("application/json")]
public sealed class HallsController : ControllerBase
{
    private readonly IHallService _hallService;

    public HallsController(IHallService hallService) => _hallService = hallService;

    /// <summary>Отримати список усіх залів.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HallResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HallResponse>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _hallService.GetAllAsync(cancellationToken));

    /// <summary>Отримати зал за ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HallResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HallResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _hallService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Додати конференц-зал.
    /// </summary>
    /// <remarks>
    /// Приклад запиту:
    ///
    ///     POST /api/v1/halls
    ///     {
    ///       "name": "Зал D",
    ///       "capacity": 40,
    ///       "baseHourlyRate": 1800,
    ///       "services": [
    ///         { "name": "Проєктор", "price": 500 },
    ///         { "name": "Wi-Fi", "price": 300 }
    ///       ]
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(HallResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HallResponse>> Create(
        [FromBody] CreateHallRequest request,
        CancellationToken cancellationToken)
    {
        var hall = await _hallService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = hall.Id }, hall);
    }

    /// <summary>Оновити інформацію про зал.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(HallResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HallResponse>> Update(
        Guid id,
        [FromBody] UpdateHallRequest request,
        CancellationToken cancellationToken)
        => Ok(await _hallService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Видалити конференц-зал (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiMessageResponse>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _hallService.DeleteAsync(id, cancellationToken);
        return Ok(new ApiMessageResponse("Зал успішно видалено.", id));
    }

    /// <summary>
    /// Пошук доступних залів за датою/часом і місткістю.
    /// </summary>
    /// <remarks>
    /// Приклад:
    ///
    ///     GET /api/v1/halls/available?start=2024-09-01T10:00:00&amp;end=2024-09-01T14:00:00&amp;requiredCapacity=50
    /// </remarks>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableHallResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AvailableHallResponse>>> SearchAvailable(
        [FromQuery] DateTimeOffset start,
        [FromQuery] DateTimeOffset end,
        [FromQuery] int requiredCapacity,
        CancellationToken cancellationToken)
    {
        var request = new SearchAvailableHallsRequest(start, end, requiredCapacity);
        return Ok(await _hallService.SearchAvailableAsync(request, cancellationToken));
    }
}
