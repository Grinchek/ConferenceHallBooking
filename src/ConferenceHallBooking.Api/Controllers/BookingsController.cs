using ConferenceHallBooking.Application.DTOs.Bookings;
using ConferenceHallBooking.Application.DTOs.Common;
using ConferenceHallBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHallBooking.Api.Controllers;

/// <summary>
/// Бронювання конференц-залів із розрахунком вартості оренди.
/// </summary>
[ApiController]
[Route("api/v1/bookings")]
[Produces("application/json")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService) => _bookingService = bookingService;

    /// <summary>Отримати бронювання за ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _bookingService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Забронювати зал. Повертає підтвердження з розбивкою вартості.
    /// </summary>
    /// <remarks>
    /// Приклад запиту:
    ///
    ///     POST /api/v1/bookings
    ///     {
    ///       "hallId": "00000000-0000-0000-0000-000000000000",
    ///       "start": "2024-09-01T10:00:00",
    ///       "end": "2024-09-01T14:00:00",
    ///       "selectedServices": ["Проєктор", "Wi-Fi"],
    ///       "customerName": "ТОВ Приклад"
    ///     }
    ///
    /// Тарифікація залу:
    /// - 06:00–09:00: −10%
    /// - 09:00–18:00: базова ставка
    /// - 12:00–14:00: +15% (пріоритет над стандартною)
    /// - 18:00–23:00: −20%
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    /// <summary>Скасувати бронювання.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiMessageResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _bookingService.CancelAsync(id, cancellationToken);
        return Ok(new ApiMessageResponse("Бронювання скасовано.", id));
    }
}
