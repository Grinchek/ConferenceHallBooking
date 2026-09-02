using System.Data;
using Microsoft.Data.SqlClient;

namespace ConferenceHallBooking.Infrastructure.Data;

public sealed class SqlSession : IAsyncDisposable, IDisposable
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private SqlConnection? _connection;
    private SqlTransaction? _transaction;

    public SqlSession(ISqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public SqlTransaction? Transaction => _transaction;

    public bool HasTransaction => _transaction is not null;

    public async Task<SqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= _connectionFactory.Create();

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        return _connection;
    }

    public async Task<SqlCommand> CreateCommandAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOpenConnectionAsync(cancellationToken);
        return CreateCommand(connection);
    }

    public SqlCommand CreateCommand(SqlConnection connection)
    {
        var command = connection.CreateCommand();
        if (_transaction is not null)
            command.Transaction = _transaction;

        return command;
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Transaction is already active.");

        var connection = await GetOpenConnectionAsync(cancellationToken);
        _transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task ExecuteTransactionalAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        if (HasTransaction)
        {
            await action(cancellationToken);
            return;
        }

        await BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            await action(cancellationToken);
            await CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
        _connection?.Dispose();
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
