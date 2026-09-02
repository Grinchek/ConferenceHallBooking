using ConferenceHallBooking.Application.DTOs.Bookings;
using ConferenceHallBooking.Application.DTOs.Halls;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Domain.Services;

namespace ConferenceHallBooking.Application.Mapping;

public static class DtoMapper
{
    public static HallResponse ToResponse(Hall hall) =>
        new(
            hall.Id,
            hall.Name,
            hall.Capacity,
            hall.BaseHourlyRate,
            hall.Services.Select(s => new ServiceDto(s.Name, s.Price)).ToList());

    public static AvailableHallResponse ToAvailableResponse(Hall hall, decimal estimatedCost) =>
        new(
            hall.Id,
            hall.Name,
            hall.Capacity,
            hall.BaseHourlyRate,
            hall.Services.Select(s => new ServiceDto(s.Name, s.Price)).ToList(),
            estimatedCost);

    public static BookingResponse ToResponse(Booking booking, PricingResult? pricing = null)
    {
        var hallName = !string.IsNullOrWhiteSpace(booking.HallName)
            ? booking.HallName
            : booking.Hall?.Name ?? "Unknown";

        return new(
            booking.Id,
            booking.HallId,
            hallName,
            booking.StartUtc,
            booking.EndUtc,
            booking.DurationHours,
            booking.CustomerName,
            booking.SelectedServices.Select(s => new ServiceCostDto(s.Name, s.Price)).ToList(),
            booking.HallRentalCost,
            booking.ServicesCost,
            booking.TotalCost,
            pricing?.Breakdown.Select(b => new PricingBreakdownDto(
                b.SegmentStart,
                b.SegmentEnd,
                b.PeriodName,
                b.Multiplier,
                b.Cost)).ToList()
            ?? []);
    }
}
