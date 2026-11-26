using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using Store.Domain.Enums;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>
/// US3-3/4, AC 11 (FR-024, research R6). Own database: cancels a seeded order. Only ORD-0008 is
/// changed, inside one test; the refused cancels leave their orders as they are.
/// </summary>
public class CancelOrderTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    [Fact]
    public async Task PendingOrder_IsCancelled_AndReturnsItsStock_OnlyOnce()
    {
        using var client = await factory.LoginDemoCustomerAsync();
        var id = await client.OrderIdAsync("ORD-0008");
        var stockBefore = await factory.StockAsync(CottonTeeBlackL);
        var before = await (await client.GetAsync($"/api/orders/{id}")).ReadOrderAsync();
        factory.Time.Advance(TimeSpan.FromMinutes(5));

        var response = await client.PostAsync($"/api/orders/{id}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.ReadOrderAsync();
        order.Status.ShouldBe("Cancelled");
        order.CanCancel.ShouldBeFalse();
        order.UpdatedAt.ShouldBeGreaterThan(before.UpdatedAt);
        order.CreatedAt.ShouldBe(before.CreatedAt);
        (await factory.StockAsync(CottonTeeBlackL)).ShouldBe(stockBefore + 2);
        (await StatusInDatabaseAsync(id)).ShouldBe(OrderStatus.Cancelled);

        var again = await client.PostAsync($"/api/orders/{id}/cancel", null);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadErrorAsync()).Code.ShouldBe("orderCannotBeCancelled");
        (await factory.StockAsync(CottonTeeBlackL)).ShouldBe(stockBefore + 2);
    }

    [Theory]
    [InlineData("ORD-0006", OrderStatus.Confirmed, "WTP-009-BLK-S")]
    [InlineData("ORD-0001", OrderStatus.Delivered, "MTS-001-WHT-M")]
    public async Task NonPendingOrder_Returns409_AndChangesNothing(string orderNumber, OrderStatus status, string itemSku)
    {
        using var client = await factory.LoginDemoCustomerAsync();
        var id = await client.OrderIdAsync(orderNumber);
        var stockBefore = await factory.StockAsync(itemSku);

        var response = await client.PostAsync($"/api/orders/{id}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorAsync()).Code.ShouldBe("orderCannotBeCancelled");
        (await StatusInDatabaseAsync(id)).ShouldBe(status);
        (await factory.StockAsync(itemSku)).ShouldBe(stockBefore);
    }

    [Fact]
    public async Task OrderPlacedByCheckout_CanBeCancelled_AndReturnsItsStock()
    {
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 3);
        var stockBefore = await factory.StockAsync(RibbedTopWhiteS);
        var placed = await (await client.CheckoutAsync()).ReadOrderAsync();
        (await factory.StockAsync(RibbedTopWhiteS)).ShouldBe(stockBefore - 3);

        var response = await client.PostAsync($"/api/orders/{placed.Id}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadOrderAsync()).Status.ShouldBe("Cancelled");
        (await factory.StockAsync(RibbedTopWhiteS)).ShouldBe(stockBefore);
    }

    [Fact]
    public async Task UnknownOrder_Returns404()
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var response = await client.PostAsync("/api/orders/999999/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("orderNotFound");
    }

    private async Task<OrderStatus> StatusInDatabaseAsync(int orderId)
    {
        await using var context = factory.CreateDbContext();
        return await context.Orders.Where(order => order.Id == orderId).Select(order => order.Status).SingleAsync();
    }
}
