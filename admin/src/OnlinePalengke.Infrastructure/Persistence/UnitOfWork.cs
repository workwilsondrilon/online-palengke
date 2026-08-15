using Microsoft.Extensions.Logging;
using OnlinePalengke.Application.Abstractions;

namespace OnlinePalengke.Infrastructure.Persistence;

/// <summary>
/// Runs a block of work inside one database transaction, committing on success and
/// rolling back on any exception.
/// </summary>
/// <remarks>
/// Every multi-table write in this application goes through here. The case that
/// motivates it is awarding quotes: the order, its awards and the resulting ledger
/// entries must land together, because a partially-written award would take a
/// customer's money without recording what any vendor is owed.
/// </remarks>
public sealed class UnitOfWork(DbSession session, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var transaction = await session.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            // Rollback is attempted on its own try so that a failure here cannot mask the
            // original exception, which is the one that actually explains what went wrong.
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackFailure)
            {
                logger.LogError(
                    rollbackFailure,
                    "Rollback failed after {OriginalError}. The connection will be discarded.",
                    ex.GetType().Name);
            }

            throw;
        }
        finally
        {
            await transaction.DisposeAsync();
            session.ClearTransaction();
        }
    }

    public Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteAsync(async ct =>
        {
            await operation(ct);
            return true;
        }, cancellationToken);
    }
}
