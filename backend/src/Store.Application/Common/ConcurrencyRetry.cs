using Microsoft.EntityFrameworkCore;

namespace Store.Application.Common;

/// <summary>
/// Runs a unit of work up to <see cref="MaxAttempts"/> times (003 research R5, R6). Each attempt
/// starts with a clean change tracker and runs in its own transaction, committed on success and
/// rolled back on any exception. A <see cref="DbUpdateConcurrencyException"/> means another request
/// changed the same rows first: the next attempt re-reads them. Any other exception ends the run.
/// </summary>
public static class ConcurrencyRetry
{
    public const int MaxAttempts = 3;

    public static async Task<T> RunAsync<T>(
        IAppDbContext db,
        Func<CancellationToken, Task<T>> attempt,
        Func<Exception> whenExhausted,
        CancellationToken cancellationToken = default)
    {
        for (var attemptNumber = 1; attemptNumber <= MaxAttempts; attemptNumber++)
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await attempt(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        db.ChangeTracker.Clear();
        throw whenExhausted();
    }
}
