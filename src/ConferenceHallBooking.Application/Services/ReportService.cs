using ConferenceHallBooking.Application.DTOs.Reports;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Enums;
using ConferenceHallBooking.Domain.Exceptions;
using ConferenceHallBooking.Domain.Services;

namespace ConferenceHallBooking.Application.Services;

/// <summary>
/// Бізнес-аналітика: виручка, завантаженість залів, популярні послуги.
/// Агрегації виконуються на рівні запитів до БД (EF → SQL).
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IHallRepository _hallRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPricingCalculator _pricingCalculator;

    public ReportService(
        IHallRepository hallRepository,
        IBookingRepository bookingRepository,
        IPricingCalculator pricingCalculator)
    {
        _hallRepository = hallRepository;
        _bookingRepository = bookingRepository;
        _pricingCalculator = pricingCalculator;
    }

    public async Task<AnalyticsSummaryDto> GetAnalyticsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var counts = await _bookingRepository.GetBookingCountsAsync(from, to, cancellationToken);
        var revenue = await GetRevenueByHallAsync(from, to, cancellationToken);
        var occupancy = await GetOccupancyAsync(from, to, cancellationToken);
        var popular = await GetPopularServicesAsync(from, to, cancellationToken);
        var byPeriod = await GetBookingsByPeriodAsync(from, to, cancellationToken);
        var halls = await _hallRepository.GetAllAsync(cancellationToken);

        return new AnalyticsSummaryDto(
            halls.Count,
            counts.TotalBookings,
            counts.ActiveBookings,
            counts.ActiveRevenue,
            counts.ActiveBookings == 0
                ? 0
                : Math.Round(counts.ActiveRevenue / counts.ActiveBookings, 2),
            revenue,
            occupancy,
            popular,
            byPeriod);
    }

    public async Task<IReadOnlyList<RevenueByHallDto>> GetRevenueByHallAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rows = await _bookingRepository.GetRevenueByHallAsync(from, to, cancellationToken);
        return rows
            .Select(r => new RevenueByHallDto(
                r.HallId,
                r.HallName,
                r.BookingsCount,
                r.TotalRevenue,
                r.HallRentalRevenue,
                r.ServicesRevenue))
            .ToList();
    }

    public async Task<IReadOnlyList<OccupancyReportDto>> GetOccupancyAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rangeStart = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeEnd = to ?? DateTime.UtcNow.Date.AddDays(1);
        var availableHours = Math.Max((decimal)(rangeEnd - rangeStart).TotalHours, 1m);

        var rows = await _bookingRepository.GetOccupancyByHallAsync(rangeStart, rangeEnd, cancellationToken);

        return rows
            .Select(row =>
            {
                var occupancy = Math.Round(row.BookedHours / availableHours * 100m, 2);
                return new OccupancyReportDto(
                    row.HallId,
                    row.HallName,
                    row.Capacity,
                    row.BookingsCount,
                    row.BookedHours,
                    Math.Min(occupancy, 100m));
            })
            .OrderByDescending(o => o.OccupancyPercent)
            .ToList();
    }

    public async Task<IReadOnlyList<PopularServiceDto>> GetPopularServicesAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rows = await _bookingRepository.GetPopularServicesAsync(from, to, cancellationToken);
        return rows
            .Select(r => new PopularServiceDto(r.ServiceName, r.TimesBooked, r.TotalRevenue))
            .ToList();
    }

    private async Task<IReadOnlyList<BookingsByPeriodDto>> GetBookingsByPeriodAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var bookings = await _bookingRepository.GetBookingsGroupedByStartHourAsync(from, to, cancellationToken);
        var counters = Enum.GetValues<PricingPeriod>()
            .ToDictionary(p => p.ToString(), _ => (Count: 0, Revenue: 0m));

        foreach (var booking in bookings)
        {
            var pricing = _pricingCalculator.CalculateHallRental(1m, booking.StartUtc, booking.StartUtc.AddMinutes(1));
            var period = pricing.Breakdown.FirstOrDefault()?.PeriodName ?? PricingPeriod.Standard.ToString();

            if (!counters.ContainsKey(period))
                counters[period] = (0, 0);

            var current = counters[period];
            counters[period] = (current.Count + 1, current.Revenue + booking.TotalCost);
        }

        return counters
            .Select(kv => new BookingsByPeriodDto(kv.Key, kv.Value.Count, kv.Value.Revenue))
            .OrderByDescending(x => x.BookingsCount)
            .ToList();
    }

    private static void EnsureValidRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from > to)
            throw new BusinessRuleException("Параметр 'from' не може бути пізніше за 'to'.");
    }
}
