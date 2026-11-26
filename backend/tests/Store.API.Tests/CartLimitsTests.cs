using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>FR-010 and the parallel-add edge case (research R8; spec Clarifications 2026-11-26).</summary>
public class CartLimitsTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private static readonly HttpStatusCode[] AllowedUnderRace =
        [HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict];

    [Fact]
    public async Task ThirtyDifferentItems_AreAccepted_TheThirtyFirstIsCartFull()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantIds = await InStockVariantIdsAsync(31);

        foreach (var variantId in variantIds.Take(30))
        {
            (await client.AddToCartAsync(variantId, 1)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var full = await client.AddToCartAsync(variantIds[30], 1);

        full.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await full.ReadErrorAsync()).Code.ShouldBe("cartFull");
        (await client.GetCartAsync()).ItemCount.ShouldBe(30);
    }

    [Fact]
    public async Task FullCart_StillAcceptsMoreOfAnExistingItem()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantIds = await InStockVariantIdsAsync(30);
        foreach (var variantId in variantIds)
        {
            await client.AddToCartAsync(variantId, 1);
        }

        var more = await client.AddToCartAsync(variantIds[0], 1);

        more.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ParallelAddsOfTheSameNewVariant_KeepOneLine_NeverA500()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var topId = await factory.VariantIdAsync(RibbedTopWhiteS);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.AddToCartAsync(topId, 1)));

        responses.ShouldAllBe(response => AllowedUnderRace.Contains(response.StatusCode));
        responses.ShouldContain(response => response.StatusCode == HttpStatusCode.OK);
        // An increment may be lost under this race (accepted); the line stays single and within 1–5.
        var line = (await client.GetCartAsync()).Items.ShouldHaveSingleItem();
        line.Quantity.ShouldBeInRange(1, 5);
    }

    private async Task<List<int>> InStockVariantIdsAsync(int count)
    {
        await using var context = factory.CreateDbContext();
        return await context.ProductVariants
            .Where(variant => variant.Stock > 0 && variant.Product.IsActive)
            .OrderBy(variant => variant.Id)
            .Select(variant => variant.Id)
            .Take(count)
            .ToListAsync();
    }
}
