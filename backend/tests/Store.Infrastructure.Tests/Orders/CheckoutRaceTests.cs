using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Orders.Requests;
using Store.Application.Orders.Services;
using Store.Application.Orders.Validators;
using Store.Domain.Entities;
using Store.Infrastructure.Orders;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding.Data;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Orders;

/// <summary>
/// Research R5: checkout conflicts forced with a save interceptor. Just before this checkout saves,
/// another "buyer" changes the variant's stock through a second connection, so the variant's
/// rowversion no longer matches and the attempt fails as a whole.
/// </summary>
public class CheckoutRaceTests : IAsyncLifetime
{
    private const string RibbedTopWhiteS = "WTP-009-WHT-S"; // stock 20, price 300
    private const string CottonTeeBlackM = "MTS-001-BLK-M"; // stock 1

    private static readonly PlaceOrderRequest Shipping = new("Demo Customer", "+201000000002", "Cairo", "12 Tahrir Street", null);

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(Seeders.SeedTime);

    private int _userId;

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
        _userId = await context.Users.Where(user => user.Email == AccountSeedData.DemoCustomerEmail).Select(user => user.Id).SingleAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task OneConflict_RetryReadsTheNewStock_AndPlacesTheOrder()
    {
        var variantId = await AddToCartAsync(RibbedTopWhiteS, 2);
        var interceptor = new OtherBuyerFirst(_db, variantId, unitsTaken: 1, times: 1);
        await using var context = CreateContext(interceptor);

        var order = await CreateService(context).CheckoutAsync(_userId, Shipping);

        interceptor.Fired.ShouldBe(1);
        order.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);
        (await StockAsync(variantId)).ShouldBe(20 - 1 - 2);
        await using var check = _db.CreateContext();
        (await check.CartItems.AnyAsync(line => line.UserId == _userId)).ShouldBeFalse();
        (await check.Orders.CountAsync(saved => saved.UserId == _userId && saved.OrderNumber == order.OrderNumber)).ShouldBe(1);
    }

    [Fact]
    public async Task EveryAttemptConflicts_Returns409CheckoutConflict_AndSavesNothing()
    {
        var variantId = await AddToCartAsync(RibbedTopWhiteS, 2);
        var interceptor = new OtherBuyerFirst(_db, variantId, unitsTaken: 1, times: ConcurrencyRetry.MaxAttempts);
        await using var context = CreateContext(interceptor);
        var ordersBefore = await OrderCountAsync();

        var error = await Should.ThrowAsync<ConflictException>(() => CreateService(context).CheckoutAsync(_userId, Shipping));

        error.Code.ShouldBe(ErrorCodes.CheckoutConflict);
        interceptor.Fired.ShouldBe(ConcurrencyRetry.MaxAttempts);
        (await StockAsync(variantId)).ShouldBe(20 - ConcurrencyRetry.MaxAttempts); // only the other buyers' units
        (await OrderCountAsync()).ShouldBe(ordersBefore);
        await using var check = _db.CreateContext();
        (await check.CartItems.SingleAsync(line => line.UserId == _userId)).Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task ConflictTakesTheLastUnit_RetryReturns409CartChanged()
    {
        var variantId = await AddToCartAsync(CottonTeeBlackM, 1);
        var interceptor = new OtherBuyerFirst(_db, variantId, unitsTaken: 1, times: 1);
        await using var context = CreateContext(interceptor);
        var ordersBefore = await OrderCountAsync();

        var error = await Should.ThrowAsync<ConflictException>(() => CreateService(context).CheckoutAsync(_userId, Shipping));

        error.Code.ShouldBe(ErrorCodes.CartChanged);
        (await StockAsync(variantId)).ShouldBe(0);
        (await OrderCountAsync()).ShouldBe(ordersBefore);
        await using var check = _db.CreateContext();
        (await check.CartItems.CountAsync(line => line.UserId == _userId)).ShouldBe(1);
    }

    private async Task<int> AddToCartAsync(string sku, int quantity)
    {
        await using var setup = _db.CreateContext();
        var variantId = await setup.ProductVariants.Where(variant => variant.Sku == sku).Select(variant => variant.Id).SingleAsync();
        setup.CartItems.Add(new CartItem(_userId, variantId, quantity, Seeders.SeedTime.UtcDateTime));
        await setup.SaveChangesAsync();
        return variantId;
    }

    private async Task<int> StockAsync(int variantId)
    {
        await using var check = _db.CreateContext();
        return await check.ProductVariants.Where(variant => variant.Id == variantId).Select(variant => variant.Stock).SingleAsync();
    }

    private async Task<int> OrderCountAsync()
    {
        await using var check = _db.CreateContext();
        return await check.Orders.CountAsync();
    }

    private AppDbContext CreateContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_db.ConnectionString)
            .AddInterceptors(interceptor)
            .Options);

    private CheckoutService CreateService(AppDbContext context) =>
        new(
            context,
            new OrderNumberGenerator(context),
            new PlaceOrderRequestValidator(),
            Options.Create(new ShopOptions()),
            _time,
            NullLogger<CheckoutService>.Instance);

    /// <summary>
    /// Before each of the first <c>times</c> checkout saves, another connection buys
    /// <c>unitsTaken</c> units of the variant, which changes its rowversion.
    /// </summary>
    private sealed class OtherBuyerFirst(TestDatabase db, int variantId, int unitsTaken, int times) : SaveChangesInterceptor
    {
        public int Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var placesOrder = eventData.Context?.ChangeTracker.Entries<Order>().Any(entry => entry.State == EntityState.Added) == true;
            if (Fired >= times || !placesOrder)
            {
                return result;
            }

            Fired++;
            await using var other = db.CreateContext();
            await other.ProductVariants
                .Where(variant => variant.Id == variantId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(variant => variant.Stock, variant => variant.Stock - unitsTaken), cancellationToken);
            return result;
        }
    }
}
