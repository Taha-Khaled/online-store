using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>US4, AC 4 (FR-012, research R9). Each test uses a new customer; no data is changed.</summary>
public class CartMergeTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const int UnknownVariantId = 99999;

    [Fact]
    public async Task GuestCart_KeepsOnlyPurchasableItems_AndMergingTwiceGivesTheSameCart()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var blackM = await factory.VariantIdAsync(CottonTeeBlackM);
        var whiteL = await factory.VariantIdAsync(CottonTeeWhiteL);
        (int, int)[] guest = [(blackM, 1), (whiteL, 1), (UnknownVariantId, 1)];

        var first = await ReadMergeAsync(await client.MergeAsync(guest));

        var line = first.Cart.Items.ShouldHaveSingleItem();
        line.VariantId.ShouldBe(blackM);
        line.Quantity.ShouldBe(1);
        first.Adjustments.ShouldBe(
        [
            new MergeAdjustment(whiteL, "outOfStock", 1, 0),
            new MergeAdjustment(UnknownVariantId, "variantNotFound", 1, 0)
        ]);

        var second = await ReadMergeAsync(await client.MergeAsync(guest));

        second.Cart.Items.ShouldBe(first.Cart.Items);
        second.Cart.Total.ShouldBe(first.Cart.Total);
        second.Adjustments.ShouldBe(first.Adjustments);
    }

    [Theory]
    [InlineData(3, 2, 3)]
    [InlineData(1, 4, 4)]
    public async Task ExistingLine_EndsAtTheHigherQuantity(int existing, int guest, int expected)
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantId = await factory.VariantIdAsync(RibbedTopWhiteS);
        await client.AddToCartAsync(variantId, existing);

        var merged = await ReadMergeAsync(await client.MergeAsync((variantId, guest)));

        merged.Cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(expected);
        merged.Adjustments.ShouldBeEmpty();
    }

    [Fact]
    public async Task MoreThanStock_IsReducedToTheStock_AndReported()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var blackM = await factory.VariantIdAsync(CottonTeeBlackM); // stock 1

        var merged = await ReadMergeAsync(await client.MergeAsync((blackM, 5)));

        merged.Cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(1);
        merged.Adjustments.ShouldBe([new MergeAdjustment(blackM, "quantityReduced", 5, 1)]);
    }

    [Fact]
    public async Task DuplicateEntries_BecomeOneLineWithTheHigherQuantity()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantId = await factory.VariantIdAsync(RibbedTopWhiteS);

        var merged = await ReadMergeAsync(await client.MergeAsync((variantId, 2), (variantId, 4), (variantId, 1)));

        merged.Cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task FullCart_SkipsNewEntriesAsCartFull_ButStillRaisesExistingLines()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantIds = await InStockVariantIdsAsync(31);
        foreach (var variantId in variantIds.Take(30))
        {
            (await client.AddToCartAsync(variantId, 1)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var merged = await ReadMergeAsync(await client.MergeAsync((variantIds[30], 1), (variantIds[0], 2)));

        merged.Adjustments.ShouldBe([new MergeAdjustment(variantIds[30], "cartFull", 1, 0)]);
        merged.Cart.ItemCount.ShouldBe(30);
        merged.Cart.Items.Single(item => item.VariantId == variantIds[0]).Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task EmptyGuestCart_ReturnsTheCurrentCart()
    {
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 2);

        var merged = await ReadMergeAsync(await client.MergeAsync());

        merged.Cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);
        merged.Adjustments.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThirtyOneEntries_Returns400()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var entries = Enumerable.Range(1, 31).Select(id => (id, 1)).ToArray();

        var response = await client.MergeAsync(entries);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("items");
    }

    [Theory]
    [InlineData(1, 6, "items[0].quantity")]
    [InlineData(1, 0, "items[0].quantity")]
    [InlineData(0, 1, "items[0].variantId")]
    public async Task InvalidEntry_Returns400WithTheField(int variantId, int quantity, string field)
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.MergeAsync((variantId, quantity));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey(field);
    }

    [Fact]
    public async Task MissingItems_Returns400()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/cart/merge", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("items");
    }

    [Fact]
    public async Task NullEntry_Returns400()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/cart/merge", new { items = new object?[] { null } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("items[0]");
    }

    [Fact]
    public async Task NullBody_Returns400()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.PostAsync(
            "/api/cart/merge",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Code.ShouldBe("validationFailed");
    }

    private static Task<MergeBody> ReadMergeAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return response.ReadJsonAsync<MergeBody>();
    }

    private async Task<List<int>> InStockVariantIdsAsync(int count)
    {
        await using var context = factory.CreateDbContext();
        return await context.ProductVariants
            .Where(variant => variant.Stock >= 2 && variant.Product.IsActive)
            .OrderBy(variant => variant.Id)
            .Select(variant => variant.Id)
            .Take(count)
            .ToListAsync();
    }
}
