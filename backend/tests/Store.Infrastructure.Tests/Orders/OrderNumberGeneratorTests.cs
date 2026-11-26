using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.Infrastructure.Orders;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Orders;

/// <summary>FR-020 / research R4: order numbers from dbo.OrderNumberSequence.</summary>
public class OrderNumberGeneratorTests : IAsyncLifetime
{
    private static readonly string[] SeededNumbers = Enumerable.Range(1, 8).Select(n => $"ORD-{n:0000}").ToArray();

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task FirstNumbers_Are1001Then1002()
    {
        await using var context = _db.CreateContext();
        var generator = new OrderNumberGenerator(context);

        (await generator.NextAsync()).ShouldBe("ORD-1001");
        (await generator.NextAsync()).ShouldBe("ORD-1002");
    }

    [Fact]
    public async Task ParallelCalls_GiveDistinctIncreasingNumbers_NeverASeededOne()
    {
        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = _db.CreateContext();
            return await new OrderNumberGenerator(context).NextAsync();
        }));

        numbers.ShouldBeUnique();
        numbers.ShouldAllBe(number => number.StartsWith("ORD-"));
        numbers.Select(number => long.Parse(number[4..])).OrderBy(value => value)
            .ShouldBe(Enumerable.Range(1001, 20).Select(value => (long)value));
        numbers.ShouldNotContain(number => SeededNumbers.Contains(number));
    }
}
