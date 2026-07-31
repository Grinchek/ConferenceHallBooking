namespace ConferenceHallBooking.Application.DTOs.Reports;

public sealed record RevenueByHallDto(
    Guid HallId,
    string HallName,
    int BookingsCount,
    decimal TotalRevenue,
    decimal HallRentalRevenue,
    decimal ServicesRevenue);

public sealed record OccupancyReportDto(
    Guid HallId,
    string HallName,
    int Capacity,
    int BookingsCount,
    decimal BookedHours,
    decimal OccupancyPercent);

public sealed record PopularServiceDto(
    string ServiceName,
    int TimesBooked,
    decimal TotalRevenue);

public sealed record BookingsByPeriodDto(
    string PeriodName,
    int BookingsCount,
    decimal TotalRevenue);

public sealed record AnalyticsSummaryDto(
    int TotalHalls,
    int TotalBookings,
    int ActiveBookings,
    decimal TotalRevenue,
    decimal AverageBookingValue,
    IReadOnlyList<RevenueByHallDto> RevenueByHall,
    IReadOnlyList<OccupancyReportDto> Occupancy,
    IReadOnlyList<PopularServiceDto> PopularServices,
    IReadOnlyList<BookingsByPeriodDto> BookingsByPeriod);
