using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Application.Common;
using Store.Domain.Entities;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Common;

/// <summary>
/// Research R5/R6: the retry shell used by checkout and cancel. Uses a real database so the
/// per-attempt transaction and rollback are exercised.
/// </summary>
public class ConcurrencyRetryTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Success_RunsOnceAndCommits()
    {
        await using var context = _db.CreateContext();
        var attempts = 0;

        var result = await ConcurrencyRetry.RunAsync(context, async ct =>
        {
            attempts++;
            await AddColorAsync(context, "Committed", ct);
            return 42;
        }, () => new InvalidOperationException("exhausted"));

        result.ShouldBe(42);
        attempts.ShouldBe(1);
        (await ColorExistsAsync("Committed")).ShouldBeTrue();
    }

    [Fact]
    public async Task Conflict_RollsBackAndRetriesWithACleanTracker()
    {
        await using var context = _db.CreateContext();
        var attempts = 0;
        var trackedAtSecondAttempt = -1;

        var result = await ConcurrencyRetry.RunAsync(context, async ct =>
        {
            attempts++;
            if (attempts == 1)
            {
                await AddColorAsync(context, "RolledBack", ct);
                context.Colors.Add(new Color("StillTracked", "#000001"));
                throw new DbUpdateConcurrencyException("simulated conflict");
            }

            trackedAtSecondAttempt = context.ChangeTracker.Entries().Count();
            return "done";
        }, () => new InvalidOperationException("exhausted"));

        result.ShouldBe("done");
        attempts.ShouldBe(2);
        trackedAtSecondAttempt.ShouldBe(0);
        (await ColorExistsAsync("RolledBack")).ShouldBeFalse();
    }

    [Fact]
    public async Task ThreeConflicts_ThrowTheSuppliedException()
    {
        await using var context = _db.CreateContext();
        var attempts = 0;

        var error = await Should.ThrowAsync<TimeoutException>(() => ConcurrencyRetry.RunAsync<int>(context, _ =>
        {
            attempts++;
            throw new DbUpdateConcurrencyException("simulated conflict");
        }, () => new TimeoutException("exhausted")));

        error.Message.ShouldBe("exhausted");
        attempts.ShouldBe(ConcurrencyRetry.MaxAttempts);
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherException_PropagatesImmediatelyAndRollsBack()
    {
        await using var context = _db.CreateContext();
        var attempts = 0;

        await Should.ThrowAsync<ArgumentException>(() => ConcurrencyRetry.RunAsync<int>(context, async ct =>
        {
            attempts++;
            await AddColorAsync(context, "NotKept", ct);
            throw new ArgumentException("business rule");
        }, () => new InvalidOperationException("exhausted")));

        attempts.ShouldBe(1);
        (await ColorExistsAsync("NotKept")).ShouldBeFalse();
    }

    private static async Task AddColorAsync(AppDbContext context, string name, CancellationToken cancellationToken)
    {
        context.Colors.Add(new Color(name, "#123456"));
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ColorExistsAsync(string name)
    {
        await using var check = _db.CreateContext();
        return await check.Colors.AnyAsync(color => color.Name == name);
    }
}
