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
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAllAsync(bool includeCancelled = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<bool> HasOverlapAsync(Guid hallId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
