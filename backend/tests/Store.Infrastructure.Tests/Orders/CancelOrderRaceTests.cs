using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Orders.Services;
using Store.Application.Orders.Validators;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding.Data;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Orders;

/// <summary>
/// Research R6: a cancel based on a stale Pending read must not overwrite a change made elsewhere
/// in between. Just before the cancel saves, a second connection changes ORD-0008, so the order's
/// rowversion no longer matches and the attempt fails as a whole.
/// </summary>
public class CancelOrderRaceTests : IAsyncLifetime
{
    private const string OrderNumber = "ORD-0008";        // Pending, Classic Cotton T-Shirt Black/L × 2
    private const string CottonTeeBlackL = "MTS-001-BLK-L";

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(Seeders.SeedTime);

    private int _userId;
    private int _orderId;

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
        _userId = await context.Users.Where(user => user.Email == AccountSeedData.DemoCustomerEmail).Select(user => user.Id).SingleAsync();
        _orderId = await context.Orders.Where(order => order.OrderNumber == OrderNumber).Select(order => order.Id).SingleAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task ConfirmedBeforeSaving_Returns409OrderCannotBeCancelled_AndKeepsTheStock()
    {
        var stockBefore = await StockAsync();
        var interceptor = new ChangeOrderFirst(
            _db,
            _orderId,
            times: 1,
            orders => orders.ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Status, OrderStatus.Confirmed)));
        await using var context = CreateContext(interceptor);

        var error = await Should.ThrowAsync<ConflictException>(() => CreateService(context).CancelAsync(_userId, _orderId));

        error.Code.ShouldBe(ErrorCodes.OrderCannotBeCancelled);
        interceptor.Fired.ShouldBe(1);
        (await StatusAsync()).ShouldBe(OrderStatus.Confirmed);
        (await StockAsync()).ShouldBe(stockBefore);
    }

    [Fact]
    public async Task ChangedBeforeEverySave_Returns409OrderConflict_AndChangesNothing()
    {
        var stockBefore = await StockAsync();
        var interceptor = new ChangeOrderFirst(
            _db,
            _orderId,
            times: ConcurrencyRetry.MaxAttempts,
            orders => orders.ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Notes, "Changed elsewhere")));
        await using var context = CreateContext(interceptor);

        var error = await Should.ThrowAsync<ConflictException>(() => CreateService(context).CancelAsync(_userId, _orderId));

        error.Code.ShouldBe(ErrorCodes.OrderConflict);
        interceptor.Fired.ShouldBe(ConcurrencyRetry.MaxAttempts);
        (await StatusAsync()).ShouldBe(OrderStatus.Pending);
        (await StockAsync()).ShouldBe(stockBefore);
    }

    private async Task<int> StockAsync()
    {
        await using var check = _db.CreateContext();
        return await check.ProductVariants.Where(variant => variant.Sku == CottonTeeBlackL).Select(variant => variant.Stock).SingleAsync();
    }

    private async Task<OrderStatus> StatusAsync()
    {
        await using var check = _db.CreateContext();
        return await check.Orders.Where(order => order.Id == _orderId).Select(order => order.Status).SingleAsync();
    }

    private AppDbContext CreateContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_db.ConnectionString)
            .AddInterceptors(interceptor)
            .Options);

    private CustomerOrderService CreateService(AppDbContext context) =>
        new(context, new OrderListQueryValidator(), _time, NullLogger<CustomerOrderService>.Instance);

    /// <summary>Before each of the first <c>times</c> saves of a modified order, another connection changes that order.</summary>
    private sealed class ChangeOrderFirst(
        TestDatabase db,
        int orderId,
        int times,
        Func<IQueryable<Order>, Task<int>> change) : SaveChangesInterceptor
    {
        public int Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var savesOrder = eventData.Context?.ChangeTracker.Entries<Order>().Any(entry => entry.State == EntityState.Modified) == true;
            if (Fired >= times || !savesOrder)
            {
                return result;
            }

            Fired++;
            await using var other = db.CreateContext();
            await change(other.Orders.Where(order => order.Id == orderId));
            return result;
        }
    }
}
