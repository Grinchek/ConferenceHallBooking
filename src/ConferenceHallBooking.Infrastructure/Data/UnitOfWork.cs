using System.Data;
using ConferenceHallBooking.Application.Interfaces;

namespace ConferenceHallBooking.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlSession _session;

    public UnitOfWork(SqlSession session) => _session = session;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _session.HasTransaction
            ? CommitAsync(cancellationToken)
            : Task.FromResult(0);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        await _session.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            await action(cancellationToken);

            if (_session.HasTransaction)
                await _session.CommitAsync(cancellationToken);
        }
        catch
        {
            await _session.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> CommitAsync(CancellationToken cancellationToken)
    {
        await _session.CommitAsync(cancellationToken);
        return 0;
    }
}
