using ConferenceHallBooking.Domain.Entities;

namespace ConferenceHallBooking.Application.Interfaces;

public interface IHallRepository
{
    Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Hall?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Hall>> SearchAvailableAsync(DateTime start, DateTime end, int requiredCapacity, CancellationToken cancellationToken = default);
    Task AddAsync(Hall hall, CancellationToken cancellationToken = default);
    Task UpdateAsync(Hall hall, CancellationToken cancellationToken = default);
    Task SetServicesAsync(Guid hallId, IEnumerable<(string Name, decimal Price)> services, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAllAsync(bool includeCancelled = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<bool> HasOverlapAsync(Guid hallId, DateTime start, DateTime end, CancellationToken cancellationToken = default);

    Task<BookingCountsRow> GetBookingCountsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HallRevenueRow>> GetRevenueByHallAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HallOccupancyRow>> GetOccupancyByHallAsync(DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PopularServiceRow>> GetPopularServicesAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeriodBookingRow>> GetBookingsGroupedByStartHourAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}

public sealed record BookingCountsRow(int TotalBookings, int ActiveBookings, decimal ActiveRevenue);

public sealed record HallRevenueRow(
    Guid HallId,
    string HallName,
    int BookingsCount,
    decimal TotalRevenue,
    decimal HallRentalRevenue,
    decimal ServicesRevenue);

public sealed record HallOccupancyRow(
    Guid HallId,
    string HallName,
    int Capacity,
    int BookingsCount,
    decimal BookedHours);

public sealed record PopularServiceRow(string ServiceName, int TimesBooked, decimal TotalRevenue);

public sealed record PeriodBookingRow(DateTime StartUtc, decimal TotalCost);
