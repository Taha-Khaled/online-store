using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>
/// AC 6, 7, 8 / SC-002 (FR-018, FR-019, research R5): real parallel checkouts never oversell. xUnit
/// creates a class instance per test, so each test gets its own factory and seeded database.
/// Assertions read orders and stock from the database.
/// </summary>
public class CheckoutConcurrencyTests : IAsyncLifetime
{
    private readonly StoreApiFactory _factory = new();

    /// <summary>Starts the app, which migrates and seeds the database, before the test reads it.</summary>
    public Task InitializeAsync()
    {
        _factory.CreateClient().Dispose();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task TwoCustomers_LastUnit_ExactlyOneOrder_OtherGetsCartChanged()
    {
        var clients = await CustomersWithOneUnitAsync(CottonTeeBlackM, 2);
        (await _factory.StockAsync(CottonTeeBlackM)).ShouldBe(1);

        var responses = await CheckoutInParallelAsync(clients.Select(customer => customer.Client));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var loser = responses.Single(response => response.StatusCode != HttpStatusCode.Created);
        loser.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await loser.ReadErrorAsync()).Code.ShouldBe("cartChanged");

        (await _factory.StockAsync(CottonTeeBlackM)).ShouldBe(0);
        (await OrderCountAsync(clients.Select(customer => customer.UserId))).ShouldBe(1);
    }

    [Fact]
    public async Task TenCustomers_ThreeUnits_ExactlyThreeOrders_SevenConflicts()
    {
        var clients = await CustomersWithOneUnitAsync(SneakersWhite43, 10);
        (await _factory.StockAsync(SneakersWhite43)).ShouldBe(3);

        var responses = await CheckoutInParallelAsync(clients.Select(customer => customer.Client));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(3);
        var losers = responses.Where(response => response.StatusCode != HttpStatusCode.Created).ToList();
        losers.Count.ShouldBe(7);
        foreach (var loser in losers)
        {
            loser.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await loser.ReadErrorAsync()).Code.ShouldBeOneOf("cartChanged", "checkoutConflict");
        }

        (await _factory.StockAsync(SneakersWhite43)).ShouldBe(0);
        (await OrderCountAsync(clients.Select(customer => customer.UserId))).ShouldBe(3);
    }

    [Fact]
    public async Task SameCustomer_TwoParallelCheckouts_AtMostOneOrder()
    {
        var (client, auth) = await _factory.NewCustomerAsync();
        await client.AddToCartAsync(await _factory.VariantIdAsync(RibbedTopWhiteS), 2);
        var stockBefore = await _factory.StockAsync(RibbedTopWhiteS);
        using var second = _factory.CreateAuthorizedClient(auth.AccessToken);

        var responses = await CheckoutInParallelAsync([client, second]);

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var loser = responses.Single(response => response.StatusCode != HttpStatusCode.Created);
        var loserCode = (await loser.ReadErrorAsync()).Code;
        if (loser.StatusCode == HttpStatusCode.BadRequest)
        {
            loserCode.ShouldBe("cartEmpty");
        }
        else
        {
            loser.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            loserCode.ShouldBeOneOf("cartChanged", "checkoutConflict");
        }

        (await _factory.StockAsync(RibbedTopWhiteS)).ShouldBe(stockBefore - 2);
        (await OrderCountAsync([auth.User.Id])).ShouldBe(1);
        (await client.GetCartAsync()).Items.ShouldBeEmpty();
    }

    /// <summary>Registers <paramref name="count"/> customers, each with one unit of the SKU in the cart.</summary>
    private async Task<List<(HttpClient Client, int UserId)>> CustomersWithOneUnitAsync(string sku, int count)
    {
        var variantId = await _factory.VariantIdAsync(sku);
        var customers = new List<(HttpClient Client, int UserId)>();
        for (var index = 0; index < count; index++)
        {
            var (client, auth) = await _factory.NewCustomerAsync();
            (await client.AddToCartAsync(variantId, 1)).StatusCode.ShouldBe(HttpStatusCode.OK);
            customers.Add((client, auth.User.Id));
        }

        return customers;
    }

    private static Task<HttpResponseMessage[]> CheckoutInParallelAsync(IEnumerable<HttpClient> clients) =>
        Task.WhenAll(clients.Select(client => Task.Run(() => client.CheckoutAsync())));

    private async Task<int> OrderCountAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.ToList();
        await using var context = _factory.CreateDbContext();
        return await context.Orders.CountAsync(order => ids.Contains(order.UserId));
    }
}
