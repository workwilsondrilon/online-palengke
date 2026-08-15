using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Options;
using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure.Persistence;

/// <summary>
/// The single database connection for the current request, and the transaction (if any)
/// currently open on it.
/// </summary>
/// <remarks>
/// Registered scoped, so every repository resolved during one request shares this
/// connection. That is what lets <see cref="UnitOfWork"/> wrap several repositories in
/// one atomic operation without any of them being aware of each other.
/// </remarks>
public sealed class DbSession(IDbConnectionFactory factory, IOptions<DatabaseOptions> options)
    : IDbSession
{
    private DbConnection? _connection;
    private bool _disposed;

    /// <summary>Command timeout in seconds, for repositories to pass to Dapper.</summary>
    public int CommandTimeoutSeconds { get; } = options.Value.CommandTimeoutSeconds;

    public DbConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "The connection is not open. Call OpenAsync before using the session.");

    public DbTransaction? Transaction { get; private set; }

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null)
        {
            _connection = factory.Create();
            await _connection.OpenAsync(cancellationToken);
        }
        else if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    /// <summary>
    /// Starts a transaction on this session's connection. Called only by
    /// <see cref="UnitOfWork"/> — application code should not manage transactions directly.
    /// </summary>
    internal async Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open on this session. Nested transactions are not supported — " +
                "compose the work into a single UnitOfWork.ExecuteAsync call instead.");
        }

        var connection = await OpenAsync(cancellationToken);
        Transaction = await connection.BeginTransactionAsync(cancellationToken);

        return Transaction;
    }

    /// <summary>Clears the ambient transaction after it has been committed or rolled back.</summary>
    internal void ClearTransaction() => Transaction = null;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Transaction is not null)
        {
            // Reaching here means an exception escaped without the UnitOfWork unwinding.
            // Rolling back is the safe default; the original exception is already propagating.
            await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
            Transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
