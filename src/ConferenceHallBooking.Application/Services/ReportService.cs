using ConferenceHallBooking.Application.DTOs.Reports;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Enums;
using ConferenceHallBooking.Domain.Services;

namespace ConferenceHallBooking.Application.Services;

/// <summary>
/// Бізнес-аналітика: виручка, завантаженість залів, популярні послуги.
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
        var revenue = await GetRevenueByHallAsync(from, to, cancellationToken);
        var occupancy = await GetOccupancyAsync(from, to, cancellationToken);
        var popular = await GetPopularServicesAsync(from, to, cancellationToken);
        var byPeriod = await GetBookingsByPeriodAsync(from, to, cancellationToken);

        var halls = await _hallRepository.GetAllAsync(cancellationToken);
        var bookings = await GetFilteredBookingsAsync(from, to, cancellationToken);

        var totalRevenue = bookings.Sum(b => b.TotalCost);
        var active = bookings.Count;

        return new AnalyticsSummaryDto(
            halls.Count,
            active,
            active,
            totalRevenue,
            active == 0 ? 0 : Math.Round(totalRevenue / active, 2),
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
        var halls = await _hallRepository.GetAllAsync(cancellationToken);
        var bookings = await GetFilteredBookingsAsync(from, to, cancellationToken);

        return halls
            .Select(hall =>
            {
                var hallBookings = bookings.Where(b => b.HallId == hall.Id).ToList();
                return new RevenueByHallDto(
                    hall.Id,
                    hall.Name,
                    hallBookings.Count,
                    hallBookings.Sum(b => b.TotalCost),
                    hallBookings.Sum(b => b.HallRentalCost),
                    hallBookings.Sum(b => b.ServicesCost));
            })
            .OrderByDescending(r => r.TotalRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<OccupancyReportDto>> GetOccupancyAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var rangeStart = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeEnd = to ?? DateTime.UtcNow.Date.AddDays(1);
        var availableHours = Math.Max((decimal)(rangeEnd - rangeStart).TotalHours, 1m);

        var halls = await _hallRepository.GetAllAsync(cancellationToken);
        var bookings = await GetFilteredBookingsAsync(rangeStart, rangeEnd, cancellationToken);

        return halls
            .Select(hall =>
            {
                var hallBookings = bookings.Where(b => b.HallId == hall.Id).ToList();
                var bookedHours = hallBookings.Sum(b => b.DurationHours);
                var occupancy = Math.Round(bookedHours / availableHours * 100m, 2);

                return new OccupancyReportDto(
                    hall.Id,
                    hall.Name,
                    hall.Capacity,
                    hallBookings.Count,
                    bookedHours,
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
        var bookings = await GetFilteredBookingsAsync(from, to, cancellationToken);

        return bookings
            .SelectMany(b => b.SelectedServices)
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PopularServiceDto(
                g.First().Name,
                g.Count(),
                g.Sum(s => s.Price)))
            .OrderByDescending(s => s.TimesBooked)
            .ToList();
    }

    private async Task<IReadOnlyList<BookingsByPeriodDto>> GetBookingsByPeriodAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var bookings = await GetFilteredBookingsAsync(from, to, cancellationToken);
        var counters = Enum.GetValues<PricingPeriod>()
            .ToDictionary(p => p.ToString(), _ => (Count: 0, Revenue: 0m));

        foreach (var booking in bookings)
        {
            // Класифікуємо бронювання за тарифним періодом старту
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

    private async Task<IReadOnlyList<Domain.Entities.Booking>> GetFilteredBookingsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Entities.Booking> bookings;

        if (from.HasValue || to.HasValue)
        {
            bookings = await _bookingRepository.GetByDateRangeAsync(
                from ?? DateTime.MinValue,
                to ?? DateTime.MaxValue,
                cancellationToken);
        }
        else
        {
            bookings = await _bookingRepository.GetAllAsync(includeCancelled: false, cancellationToken);
        }

        return bookings.Where(b => !b.IsCancelled).ToList();
    }
}
