using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Store.API.Tests.Fixtures;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>US1 / AC 2, 3: the customer's server cart (FR-003–FR-011). A fresh customer per test.</summary>
public class CartTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    [Fact]
    public async Task AddTwo_ThenThree_ThenOneMore_StopsAtFive()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var topId = await factory.VariantIdAsync(RibbedTopWhiteS);

        var first = await (await client.AddToCartAsync(topId, 2)).ReadCartAsync();

        var line = first.Items.ShouldHaveSingleItem();
        line.Quantity.ShouldBe(2);
        line.UnitPrice.ShouldBe(300m);
        line.LineTotal.ShouldBe(600m);
        line.Status.ShouldBe("available");
        first.ItemCount.ShouldBe(1);
        first.Subtotal.ShouldBe(600m);
        first.ShippingFee.ShouldBe(50m);
        first.Total.ShouldBe(650m);
        first.CanCheckout.ShouldBeTrue();

        var five = await (await client.AddToCartAsync(topId, 3)).ReadCartAsync();
        five.Items.ShouldHaveSingleItem().Quantity.ShouldBe(5);

        var sixth = await client.AddToCartAsync(topId, 1);
        sixth.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await sixth.ReadErrorAsync()).Code.ShouldBe("maxQuantityExceeded");
        (await client.GetCartAsync()).Items.ShouldHaveSingleItem().Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task BlackMWithStockOne_TwoIsRefusedWithMaxQuantity_OneIsAccepted()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var blackM = await factory.VariantIdAsync(CottonTeeBlackM);

        var two = await client.AddToCartAsync(blackM, 2);

        two.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await two.ReadErrorAsync()).Code.ShouldBe("insufficientStock");
        var details = await two.ReadErrorDetailsAsync();
        details.GetProperty("variantId").GetInt32().ShouldBe(blackM);
        details.GetProperty("maxQuantity").GetInt32().ShouldBe(1);

        var one = await client.AddToCartAsync(blackM, 1);
        one.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await one.ReadCartAsync()).Items.ShouldHaveSingleItem().MaxQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task OutOfStockVariant_IsRefusedWithMaxQuantityZero()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.AddToCartAsync(await factory.VariantIdAsync(CottonTeeWhiteL), 1);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorAsync()).Code.ShouldBe("insufficientStock");
        (await response.ReadErrorDetailsAsync()).GetProperty("maxQuantity").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task EmptyCart_HasZeroTotalsAndCannotCheckOut()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var cart = await client.GetCartAsync();

        cart.Items.ShouldBeEmpty();
        cart.ItemCount.ShouldBe(0);
        cart.Subtotal.ShouldBe(0m);
        cart.ShippingFee.ShouldBe(0m);
        cart.Total.ShouldBe(0m);
        cart.CanCheckout.ShouldBeFalse();
    }

    [Fact]
    public async Task Line_ShowsProductColorSizeAndImage_ButNeverStock()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1);

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotContain("\"stock\"", Case.Sensitive);
        var line = (await response.ReadCartAsync()).Items.ShouldHaveSingleItem();
        line.ProductName.ShouldBe("Basic Ribbed Top");
        line.Slug.ShouldBe("basic-ribbed-top");
        line.ColorName.ShouldBe("White");
        line.SizeName.ShouldBe("S");
        line.ImageUrl.ShouldBe("/images/products/basic-ribbed-top-white.jpg");
        line.MaxQuantity.ShouldBe(5);
    }

    [Fact]
    public async Task Update_Remove_Clear_ChangeTheCart()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var top = await (await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1)).ReadCartAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(CottonTeeBlackM), 1);
        var topItemId = top.Items[0].ItemId;

        var updated = await (await client.PutAsJsonAsync($"/api/cart/items/{topItemId}", new { quantity = 3 })).ReadCartAsync();
        updated.Items.Single(item => item.ItemId == topItemId).Quantity.ShouldBe(3);
        updated.Subtotal.ShouldBe(1250m);

        var removed = await (await client.DeleteAsync($"/api/cart/items/{topItemId}")).ReadCartAsync();
        removed.Items.ShouldHaveSingleItem().ProductName.ShouldBe("Classic Cotton T-Shirt");

        (await client.DeleteAsync("/api/cart")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetCartAsync()).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAboveStock_Returns409InsufficientStock()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var cart = await (await client.AddToCartAsync(await factory.VariantIdAsync(CottonTeeBlackM), 1)).ReadCartAsync();

        var response = await client.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].ItemId}", new { quantity = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorDetailsAsync()).GetProperty("maxQuantity").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task AnotherCustomersItem_Returns404CartItemNotFound()
    {
        var (owner, _) = await factory.NewCustomerAsync();
        var (stranger, _) = await factory.NewCustomerAsync();
        var itemId = (await (await owner.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1)).ReadCartAsync()).Items[0].ItemId;

        var update = await stranger.PutAsJsonAsync($"/api/cart/items/{itemId}", new { quantity = 2 });
        var delete = await stranger.DeleteAsync($"/api/cart/items/{itemId}");

        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await update.ReadErrorAsync()).Code.ShouldBe("cartItemNotFound");
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await delete.ReadErrorAsync()).Code.ShouldBe("cartItemNotFound");
        (await owner.GetCartAsync()).Items.ShouldHaveSingleItem().Quantity.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task QuantityOutsideOneToFive_Returns400(int quantity)
    {
        var (client, _) = await factory.NewCustomerAsync();
        var topId = await factory.VariantIdAsync(RibbedTopWhiteS);

        var add = await client.AddToCartAsync(topId, quantity);
        var itemId = (await (await client.AddToCartAsync(topId, 1)).ReadCartAsync()).Items[0].ItemId;
        var update = await client.PutAsJsonAsync($"/api/cart/items/{itemId}", new { quantity });

        add.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await add.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("quantity");
        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await update.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("quantity");
    }

    [Fact]
    public async Task UnknownVariant_Returns404VariantNotFound()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.AddToCartAsync(999999, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("variantNotFound");
    }

    [Fact]
    public async Task UnknownItem_Returns404()
    {
        var (client, _) = await factory.NewCustomerAsync();

        (await client.PutAsJsonAsync("/api/cart/items/999999", new { quantity = 1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync("/api/cart/items/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CartOperations_NeverChangeStock()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var before = await factory.StockAsync(RibbedTopWhiteS);

        var cart = await (await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 2)).ReadCartAsync();
        await client.PutAsJsonAsync($"/api/cart/items/{cart.Items[0].ItemId}", new { quantity = 4 });
        await client.DeleteAsync($"/api/cart/items/{cart.Items[0].ItemId}");
        await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1);
        await client.DeleteAsync("/api/cart");

        (await factory.StockAsync(RibbedTopWhiteS)).ShouldBe(before);
    }
}
