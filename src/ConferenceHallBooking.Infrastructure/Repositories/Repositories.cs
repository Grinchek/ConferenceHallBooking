using System.Data;
using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public sealed class HallRepository : IHallRepository
{
    private readonly AppDbContext _db;

    public HallRepository(AppDbContext db) => _db = db;

    public Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Halls.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public Task<Hall?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Halls
            .AsNoTracking()
            .Include(h => h.Services)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Halls
            .AsNoTracking()
            .Include(h => h.Services)
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Hall>> SearchAvailableAsync(
        DateTime start,
        DateTime end,
        int requiredCapacity,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _db.Halls
            .AsNoTracking()
            .Include(h => h.Services)
            .Where(h => h.Capacity >= requiredCapacity)
            .ToListAsync(cancellationToken);

        var hallIds = candidates.Select(h => h.Id).ToList();
        var busyHallIds = await _db.Bookings
            .Where(b => hallIds.Contains(b.HallId)
                        && !b.IsCancelled
                        && b.StartUtc < end
                        && b.EndUtc > start)
            .Select(b => b.HallId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return candidates
            .Where(h => !busyHallIds.Contains(h.Id))
            .OrderBy(h => h.BaseHourlyRate)
            .ToList();
    }

    public async Task AddAsync(Hall hall, CancellationToken cancellationToken = default) =>
        await _db.Halls.AddAsync(hall, cancellationToken);

    public Task UpdateAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(hall);
        if (entry.State == EntityState.Detached)
            _db.Halls.Update(hall);

        return Task.CompletedTask;
    }

    public async Task SetServicesAsync(
        Guid hallId,
        IEnumerable<(string Name, decimal Price)> services,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.HallServices
            .Where(s => s.HallId == hallId)
            .ToListAsync(cancellationToken);

        _db.HallServices.RemoveRange(existing);

        var distinct = services
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var service in distinct)
            await _db.HallServices.AddAsync(new HallService(service.Name, service.Price, hallId), cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Halls.Where(h => h.Name.ToLower() == name.Trim().ToLower());
        if (excludeId.HasValue)
            query = query.Where(h => h.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }
}

public sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _db;

    public BookingRepository(AppDbContext db) => _db = db;

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Bookings
            .Include(b => b.Hall)
            .Include(b => b.SelectedServices)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetAllAsync(bool includeCancelled = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Bookings
            .Include(b => b.Hall)
            .Include(b => b.SelectedServices)
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(b => !b.IsCancelled);

        return await query.OrderByDescending(b => b.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default) =>
        await _db.Bookings
            .Include(b => b.Hall)
            .Include(b => b.SelectedServices)
            .Where(b => !b.IsCancelled && b.StartUtc < to && b.EndUtc > from)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default) =>
        await _db.Bookings.AddAsync(booking, cancellationToken);

    public Task<bool> HasOverlapAsync(
        Guid hallId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default) =>
        _db.Bookings.AnyAsync(
            b => b.HallId == hallId
                 && !b.IsCancelled
                 && b.StartUtc < end
                 && b.EndUtc > start,
            cancellationToken);

    public async Task<BookingCountsRow> GetBookingCountsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = FilterByRange(_db.Bookings.AsNoTracking(), from, to);

        var total = await query.CountAsync(cancellationToken);
        var active = await query.CountAsync(b => !b.IsCancelled, cancellationToken);
        var revenue = await query.Where(b => !b.IsCancelled).SumAsync(b => (decimal?)b.TotalCost, cancellationToken) ?? 0m;

        return new BookingCountsRow(total, active, revenue);
    }

    public async Task<IReadOnlyList<HallRevenueRow>> GetRevenueByHallAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var activeBookings = FilterByRange(_db.Bookings.AsNoTracking(), from, to)
            .Where(b => !b.IsCancelled);

        var aggregated = await activeBookings
            .GroupBy(b => b.HallId)
            .Select(g => new
            {
                HallId = g.Key,
                BookingsCount = g.Count(),
                TotalRevenue = g.Sum(b => b.TotalCost),
                HallRentalRevenue = g.Sum(b => b.HallRentalCost),
                ServicesRevenue = g.Sum(b => b.ServicesCost)
            })
            .ToListAsync(cancellationToken);

        var halls = await _db.Halls.AsNoTracking().ToListAsync(cancellationToken);
        var byHallId = aggregated.ToDictionary(x => x.HallId);

        return halls
            .Select(hall =>
            {
                if (!byHallId.TryGetValue(hall.Id, out var row))
                    return new HallRevenueRow(hall.Id, hall.Name, 0, 0, 0, 0);

                return new HallRevenueRow(
                    hall.Id,
                    hall.Name,
                    row.BookingsCount,
                    row.TotalRevenue,
                    row.HallRentalRevenue,
                    row.ServicesRevenue);
            })
            .OrderByDescending(r => r.TotalRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<HallOccupancyRow>> GetOccupancyByHallAsync(
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        // Тільки перетин бронювання з періодом звіту (кліпінг у SQL через умовні вирази).
        var overlaps = await _db.Bookings.AsNoTracking()
            .Where(b => !b.IsCancelled && b.StartUtc < rangeEnd && b.EndUtc > rangeStart)
            .Select(b => new
            {
                b.HallId,
                OverlapStart = b.StartUtc > rangeStart ? b.StartUtc : rangeStart,
                OverlapEnd = b.EndUtc < rangeEnd ? b.EndUtc : rangeEnd
            })
            .ToListAsync(cancellationToken);

        var hoursByHall = overlaps
            .GroupBy(x => x.HallId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Count: g.Count(),
                    Hours: g.Sum(x => (decimal)(x.OverlapEnd - x.OverlapStart).TotalHours)));

        var halls = await _db.Halls.AsNoTracking().ToListAsync(cancellationToken);

        return halls
            .Select(hall =>
            {
                hoursByHall.TryGetValue(hall.Id, out var stats);
                return new HallOccupancyRow(
                    hall.Id,
                    hall.Name,
                    hall.Capacity,
                    stats.Count,
                    Math.Round(stats.Hours, 2, MidpointRounding.AwayFromZero));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PopularServiceRow>> GetPopularServicesAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var bookingIds = FilterByRange(_db.Bookings.AsNoTracking(), from, to)
            .Where(b => !b.IsCancelled)
            .Select(b => b.Id);

        return await _db.BookingServiceItems.AsNoTracking()
            .Where(s => bookingIds.Contains(s.BookingId))
            .GroupBy(s => s.Name)
            .Select(g => new PopularServiceRow(
                g.Key,
                g.Count(),
                g.Sum(s => s.Price)))
            .OrderByDescending(s => s.TimesBooked)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PeriodBookingRow>> GetBookingsGroupedByStartHourAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        return await FilterByRange(_db.Bookings.AsNoTracking(), from, to)
            .Where(b => !b.IsCancelled)
            .Select(b => new PeriodBookingRow(b.StartUtc, b.TotalCost))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Booking> FilterByRange(IQueryable<Booking> query, DateTime? from, DateTime? to)
    {
        if (from.HasValue)
            query = query.Where(b => b.EndUtc > from.Value);

        if (to.HasValue)
            query = query.Where(b => b.StartUtc < to.Value);

        return query;
    }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction =
            await _db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
