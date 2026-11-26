using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Store.Application.Cart;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Domain.Entities;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Seeding.Data;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Cart;

/// <summary>
/// Research R8: cart line races forced with a save interceptor, because parallel HTTP requests
/// rarely collide at the exact moment. Another "request" changes the line through a second
/// connection just before this service saves.
/// </summary>
public class CartRaceTests : IAsyncLifetime
{
    private const string Sku = "WTP-009-WHT-S"; // Basic Ribbed Top White/S, stock 20

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(Seeders.SeedTime);

    private int _userId;
    private int _variantId;

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
        await Seeders.CreateDatabaseSeeder(context, Seeders.DefaultOptions()).SeedAsync();
        _userId = await context.Users.Where(user => user.Email == AccountSeedData.DemoCustomerEmail).Select(user => user.Id).SingleAsync();
        _variantId = await context.ProductVariants.Where(variant => variant.Sku == Sku).Select(variant => variant.Id).SingleAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task AddNewLine_WhenAnotherRequestInsertedItFirst_AddsToThatLine()
    {
        await using var context = CreateContext(new InsertSameLineFirst(_db, _userId, _variantId));

        var cart = await CreateService(context).AddItemAsync(_userId, new AddCartItemRequest(_variantId, 2));

        // The other request's line (×1) won the insert; this add was applied to it once (1 + 2).
        cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        await using var check = _db.CreateContext();
        (await check.CartItems.CountAsync(line => line.UserId == _userId)).ShouldBe(1);
        (await check.CartItems.SingleAsync(line => line.UserId == _userId)).Quantity.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateLine_DeletedByAnotherRequestBeforeSaving_Returns404CartItemNotFound()
    {
        int itemId;
        await using (var setup = _db.CreateContext())
        {
            var line = new CartItem(_userId, _variantId, 1, Seeders.SeedTime.UtcDateTime);
            setup.CartItems.Add(line);
            await setup.SaveChangesAsync();
            itemId = line.Id;
        }

        await using var context = CreateContext(new DeleteLineFirst(_db, itemId));

        var error = await Should.ThrowAsync<NotFoundException>(
            () => CreateService(context).UpdateItemAsync(_userId, itemId, new UpdateCartItemRequest(3)));

        error.Code.ShouldBe(ErrorCodes.CartItemNotFound);
        await using var check = _db.CreateContext();
        (await check.CartItems.AnyAsync(line => line.UserId == _userId)).ShouldBeFalse();
    }

    private AppDbContext CreateContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_db.ConnectionString)
            .AddInterceptors(interceptor)
            .Options);

    private CartService CreateService(AppDbContext context) =>
        new(
            context,
            new CartReader(context, Options.Create(new ShopOptions())),
            new AddCartItemRequestValidator(),
            new UpdateCartItemRequestValidator(),
            _time,
            NullLogger<CartService>.Instance);

    private static bool Saves(DbContextEventData eventData, EntityState state) =>
        eventData.Context is { } context
        && context.ChangeTracker.Entries<CartItem>().Any(entry => entry.State == state);

    /// <summary>Before the first insert of a cart line, another connection inserts the same line (×1).</summary>
    private sealed class InsertSameLineFirst(TestDatabase db, int userId, int variantId) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_done || !Saves(eventData, EntityState.Added))
            {
                return result;
            }

            _done = true;
            await using var other = db.CreateContext();
            other.CartItems.Add(new CartItem(userId, variantId, 1, Seeders.SeedTime.UtcDateTime));
            await other.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    /// <summary>Before the first update of a cart line, another connection deletes it.</summary>
    private sealed class DeleteLineFirst(TestDatabase db, int itemId) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_done || !Saves(eventData, EntityState.Modified))
            {
                return result;
            }

            _done = true;
            await using var other = db.CreateContext();
            await other.CartItems.Where(line => line.Id == itemId).ExecuteDeleteAsync(cancellationToken);
            return result;
        }
    }
}
