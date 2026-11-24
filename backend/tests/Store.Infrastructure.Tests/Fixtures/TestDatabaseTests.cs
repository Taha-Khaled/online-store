using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;

namespace Store.Infrastructure.Tests.Fixtures;

public class TestDatabaseTests
{
    [Fact]
    public async Task Database_IsUniquePerInstance_AndDroppedOnDispose()
    {
        var first = new TestDatabase();
        var second = new TestDatabase();
        first.DatabaseName.ShouldNotBe(second.DatabaseName);
        first.DatabaseName.ShouldStartWith("StoreTests_");

        await using (var context = first.CreateContext())
        {
            await context.GetService<IRelationalDatabaseCreator>().CreateAsync();
            (await context.Database.CanConnectAsync()).ShouldBeTrue();
        }

        await first.DisposeAsync();

        await using var check = first.CreateContext();
        (await check.Database.CanConnectAsync()).ShouldBeFalse();
    }
}
