using ConferenceHallBooking.Application.DTOs.Reports;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Enums;
using ConferenceHallBooking.Domain.Exceptions;
using ConferenceHallBooking.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportService(
        IHallRepository hallRepository,
        IBookingRepository bookingRepository,
        IPricingCalculator pricingCalculator,
        IServiceScopeFactory scopeFactory)
    {
        _hallRepository = hallRepository;
        _bookingRepository = bookingRepository;
        _pricingCalculator = pricingCalculator;
        _scopeFactory = scopeFactory;
    }

    public async Task<AnalyticsSummaryDto> GetAnalyticsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rangeStart = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeEnd = to ?? DateTime.UtcNow.Date.AddDays(1);

        // Незалежні запити до БД запускаємо паралельно; кожен — у своєму scope,
        // бо один DbContext не підтримує одночасні операції.
        var countsTask = RunInScopeAsync(
            (bookingRepository, ct) => bookingRepository.GetBookingCountsAsync(from, to, ct),
            cancellationToken);

        var revenueRowsTask = RunInScopeAsync(
            (bookingRepository, ct) => bookingRepository.GetRevenueByHallAsync(from, to, ct),
            cancellationToken);

        var occupancyRowsTask = RunInScopeAsync(
            (bookingRepository, ct) => bookingRepository.GetOccupancyByHallAsync(rangeStart, rangeEnd, ct),
            cancellationToken);

        var popularRowsTask = RunInScopeAsync(
            (bookingRepository, ct) => bookingRepository.GetPopularServicesAsync(from, to, ct),
            cancellationToken);

        var periodBookingsTask = RunInScopeAsync(
            (bookingRepository, ct) => bookingRepository.GetBookingsGroupedByStartHourAsync(from, to, ct),
            cancellationToken);

        var hallsTask = RunInScopeAsync(
            (hallRepository, ct) => hallRepository.GetAllAsync(ct),
            cancellationToken);

        await Task.WhenAll(
            countsTask,
            revenueRowsTask,
            occupancyRowsTask,
            popularRowsTask,
            periodBookingsTask,
            hallsTask);

        var counts = await countsTask;
        var revenue = MapRevenue(await revenueRowsTask);
        var occupancy = MapOccupancy(await occupancyRowsTask, rangeStart, rangeEnd);
        var popular = MapPopular(await popularRowsTask);
        var byPeriod = MapBookingsByPeriod(await periodBookingsTask);
        var halls = await hallsTask;

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
        return MapRevenue(rows);
    }

    public async Task<IReadOnlyList<OccupancyReportDto>> GetOccupancyAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rangeStart = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeEnd = to ?? DateTime.UtcNow.Date.AddDays(1);
        var rows = await _bookingRepository.GetOccupancyByHallAsync(rangeStart, rangeEnd, cancellationToken);

        return MapOccupancy(rows, rangeStart, rangeEnd);
    }

    public async Task<IReadOnlyList<PopularServiceDto>> GetPopularServicesAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRange(from, to);

        var rows = await _bookingRepository.GetPopularServicesAsync(from, to, cancellationToken);
        return MapPopular(rows);
    }

    private async Task<T> RunInScopeAsync<T>(
        Func<IBookingRepository, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        return await action(bookingRepository, cancellationToken);
    }

    private async Task<T> RunInScopeAsync<T>(
        Func<IHallRepository, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var hallRepository = scope.ServiceProvider.GetRequiredService<IHallRepository>();
        return await action(hallRepository, cancellationToken);
    }

    private static IReadOnlyList<RevenueByHallDto> MapRevenue(IReadOnlyList<HallRevenueRow> rows) =>
        rows
            .Select(r => new RevenueByHallDto(
                r.HallId,
                r.HallName,
                r.BookingsCount,
                r.TotalRevenue,
                r.HallRentalRevenue,
                r.ServicesRevenue))
            .ToList();

    private static IReadOnlyList<OccupancyReportDto> MapOccupancy(
        IReadOnlyList<HallOccupancyRow> rows,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var availableHours = Math.Max((decimal)(rangeEnd - rangeStart).TotalHours, 1m);

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

    private static IReadOnlyList<PopularServiceDto> MapPopular(IReadOnlyList<PopularServiceRow> rows) =>
        rows
            .Select(r => new PopularServiceDto(r.ServiceName, r.TimesBooked, r.TotalRevenue))
            .ToList();

    private IReadOnlyList<BookingsByPeriodDto> MapBookingsByPeriod(IReadOnlyList<PeriodBookingRow> bookings)
    {
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
