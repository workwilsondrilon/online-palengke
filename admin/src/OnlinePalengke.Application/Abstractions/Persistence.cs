using System.Data.Common;

namespace OnlinePalengke.Application.Abstractions;

/// <summary>Creates raw, unopened connections to the application database.</summary>
/// <remarks>
/// Used by the health check and by <see cref="IDbSession"/>. Application code should
/// almost always take <see cref="IDbSession"/> instead, so that it participates in the
/// ambient transaction rather than opening a second connection outside it.
/// </remarks>
public interface IDbConnectionFactory
{
    DbConnection Create();
}

/// <summary>
/// The one database connection for the current request, plus whatever transaction is
/// currently open on it.
/// </summary>
/// <remarks>
/// Registered scoped. Every repository takes this rather than a factory, which is what
/// makes <see cref="IUnitOfWork"/> able to wrap several repository calls in one atomic
/// unit without any of them knowing about each other. Repositories MUST pass
/// <see cref="Transaction"/> to Dapper on every call, or their writes will silently
/// fall outside the transaction.
/// </remarks>
public interface IDbSession : IAsyncDisposable
{
    /// <summary>The open connection. Call <see cref="OpenAsync"/> before first use.</summary>
    DbConnection Connection { get; }

    /// <summary>The ambient transaction, or null when not inside one.</summary>
    DbTransaction? Transaction { get; }

    /// <summary>Opens the connection if it is not already open. Safe to call repeatedly.</summary>
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs a block of work inside a single database transaction.
/// </summary>
/// <remarks>
/// Anything that writes more than one row across more than one table goes through here.
/// The motivating case is awarding a quote: the order, the awards and the ledger entries
/// must all land together or not at all, because a half-written award would charge a
/// customer without crediting a vendor.
/// </remarks>
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
