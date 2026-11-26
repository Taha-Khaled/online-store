using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US1-5/6 / AC 9 (cart part): availability and price follow current data (FR-003–FR-005). Own
/// database; data is changed directly, one product per test.
/// </summary>
public class CartAvailabilityTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    [Fact]
    public async Task DeactivatedProduct_IsUnavailable_AndCannotBeUpdated()
    {
        const string sku = "WTP-010-WHT-S"; // Satin Blouse White/S
        var (client, _) = await factory.NewCustomerAsync();
        var itemId = (await (await client.AddToCartAsync(await factory.VariantIdAsync(sku), 1)).ReadCartAsync()).Items[0].ItemId;

        await using (var context = factory.CreateDbContext())
        {
            await context.Products.Where(product => product.Slug == "satin-blouse")
                .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.IsActive, false));
        }

        var cart = await client.GetCartAsync();
        cart.Items.ShouldHaveSingleItem().Status.ShouldBe("unavailable");
        cart.CanCheckout.ShouldBeFalse();
        cart.Subtotal.ShouldBe(0m);
        cart.ShippingFee.ShouldBe(0m);

        var update = await client.PutAsJsonAsync($"/api/cart/items/{itemId}", new { quantity = 2 });
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await update.ReadErrorAsync()).Code.ShouldBe("variantNotFound");
    }

    [Fact]
    public async Task StockZero_IsOutOfStockWithMaxQuantityZero()
    {
        const string sku = "MTS-002-GRY-L"; // Graphic Oversized Tee Gray/L
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(sku), 2);

        await SetStockAsync(sku, 0);

        var line = (await client.GetCartAsync()).Items.ShouldHaveSingleItem();
        line.Status.ShouldBe("outOfStock");
        line.MaxQuantity.ShouldBe(0);
        line.Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task StockBelowQuantity_IsQuantityReduced_AndLeftOutOfTheSubtotal()
    {
        const string reducedSku = "MSH-004-BEG-M";  // Linen Summer Shirt Beige/M, 850
        const string availableSku = "WPT-012-BLU-S"; // High Waist Jeans Blue/S, 1050
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(reducedSku), 3);
        await client.AddToCartAsync(await factory.VariantIdAsync(availableSku), 1);

        await SetStockAsync(reducedSku, 1);

        var cart = await client.GetCartAsync();
        var reduced = cart.Items.Single(item => item.ProductName == "Linen Summer Shirt");
        reduced.Status.ShouldBe("quantityReduced");
        reduced.MaxQuantity.ShouldBe(1);
        reduced.Quantity.ShouldBe(3);
        cart.Items.Single(item => item.ProductName == "High Waist Jeans").Status.ShouldBe("available");
        cart.Subtotal.ShouldBe(1050m);
        cart.ShippingFee.ShouldBe(50m);
        cart.Total.ShouldBe(1100m);
        cart.CanCheckout.ShouldBeFalse();
    }

    [Fact]
    public async Task PriceChange_ShowsTheCurrentPrice()
    {
        const string sku = "MPT-005-NVY-M"; // Slim Fit Chinos Navy/M, 900
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(sku), 2);

        await using (var context = factory.CreateDbContext())
        {
            await context.Products.Where(product => product.Slug == "slim-fit-chinos")
                .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.Price, 999m));
        }

        var cart = await client.GetCartAsync();
        var line = cart.Items.ShouldHaveSingleItem();
        line.UnitPrice.ShouldBe(999m);
        line.LineTotal.ShouldBe(1998m);
        cart.Subtotal.ShouldBe(1998m);
        cart.CanCheckout.ShouldBeTrue();
    }

    private async Task SetStockAsync(string sku, int stock)
    {
        await using var context = factory.CreateDbContext();
        await context.ProductVariants.Where(variant => variant.Sku == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(variant => variant.Stock, stock));
    }
}
