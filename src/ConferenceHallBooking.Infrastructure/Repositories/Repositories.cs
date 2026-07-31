using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Domain.Entities;
using ConferenceHallBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConferenceHallBooking.Infrastructure.Repositories;

public sealed class HallRepository : IHallRepository
{
    private readonly AppDbContext _db;

    public HallRepository(AppDbContext db) => _db = db;

    public Task<Hall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Halls.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public Task<Hall?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Halls
            .Include(h => h.Services)
            .Include(h => h.Bookings.Where(b => !b.IsCancelled))
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Hall>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Halls
            .Include(h => h.Services)
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Hall>> SearchAvailableAsync(
        DateTime start,
        DateTime end,
        int requiredCapacity,
        CancellationToken cancellationToken = default)
    {
        // Кандидати за місткістю
        var candidates = await _db.Halls
            .Include(h => h.Services)
            .Include(h => h.Bookings.Where(b => !b.IsCancelled))
            .Where(h => h.Capacity >= requiredCapacity)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(h => h.IsAvailable(start, end))
            .OrderBy(h => h.BaseHourlyRate)
            .ToList();
    }

    public async Task AddAsync(Hall hall, CancellationToken cancellationToken = default) =>
        await _db.Halls.AddAsync(hall, cancellationToken);

    public Task UpdateAsync(Hall hall, CancellationToken cancellationToken = default)
    {
        _db.Halls.Update(hall);
        return Task.CompletedTask;
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
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
