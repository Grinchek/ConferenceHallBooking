namespace ConferenceHallBooking.Application.DTOs.Bookings;

public sealed record CreateBookingRequest(
    Guid HallId,
    DateTime Start,
    DateTime End,
    IReadOnlyList<string>? SelectedServices,
    string? CustomerName);

public sealed record PricingBreakdownDto(
    DateTime SegmentStart,
    DateTime SegmentEnd,
    string PeriodName,
    decimal Multiplier,
    decimal Cost);

public sealed record BookingResponse(
    Guid Id,
    Guid HallId,
    string HallName,
    DateTime Start,
    DateTime End,
    decimal DurationHours,
    string? CustomerName,
    IReadOnlyList<ServiceCostDto> SelectedServices,
    decimal HallRentalCost,
    decimal ServicesCost,
    decimal TotalCost,
    IReadOnlyList<PricingBreakdownDto> PricingBreakdown);

public sealed record ServiceCostDto(string Name, decimal Price);
