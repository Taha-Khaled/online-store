using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;
using Store.Domain.Enums;
using static Store.API.Tests.Fixtures.CartRecords;

namespace Store.API.Tests;

/// <summary>
/// US2-1/5/6/7, AC 5, 9 (checkout part) and 12 (FR-014–FR-017, FR-020). Own database: checkouts
/// change stock and some tests change product data directly.
/// </summary>
public class CheckoutTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const string SatinBlouseWhiteS = "WTP-010-WHT-S";   // product "satin-blouse"
    private const string LinenShirtBeigeM = "MSH-004-BEG-M";
    private const string GraphicTeeGrayL = "MTS-002-GRY-L";
    private const string ChinosNavyM = "MPT-005-NVY-M";

    [Fact]
    public async Task CartWithTwoUnits_CreatesPendingOrder_DeductsStock_AndEmptiesTheCart()
    {
        var (client, auth) = await factory.NewCustomerAsync();
        var variantId = await factory.VariantIdAsync(RibbedTopWhiteS);
        await client.AddToCartAsync(variantId, 2);
        var stockBefore = await factory.StockAsync(RibbedTopWhiteS);

        var response = await client.CheckoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.ReadOrderAsync();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/api/orders/{order.Id}");
        order.Status.ShouldBe("Pending");
        order.OrderNumber.ShouldStartWith("ORD-");
        OrderRecords.NumberOf(order.OrderNumber).ShouldBeGreaterThanOrEqualTo(1001);
        order.Subtotal.ShouldBe(600m);
        order.ShippingFee.ShouldBe(50m);
        order.Total.ShouldBe(650m);
        order.CanCancel.ShouldBeTrue();
        order.ShippingName.ShouldBe("Test Customer");
        order.Notes.ShouldBeNull();
        order.CreatedAt.ShouldBe(order.UpdatedAt);

        var line = order.Items.ShouldHaveSingleItem();
        line.ShouldBe(new OrderLine(variantId, "Basic Ribbed Top", "White", "S", 300m, 2, 600m));

        (await factory.StockAsync(RibbedTopWhiteS)).ShouldBe(stockBefore - 2);
        (await client.GetCartAsync()).Items.ShouldBeEmpty();

        await using var context = factory.CreateDbContext();
        var stored = await context.Orders.Include(saved => saved.Items).SingleAsync(saved => saved.Id == order.Id);
        stored.UserId.ShouldBe(auth.User.Id);
        stored.Status.ShouldBe(OrderStatus.Pending);
        stored.Total.ShouldBe(650m);
        stored.Items.ShouldHaveSingleItem().UnitPrice.ShouldBe(300m);
    }

    [Fact]
    public async Task EmptyCart_Returns400CartEmpty()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.CheckoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Code.ShouldBe("cartEmpty");
    }

    [Fact]
    public async Task InvalidShipping_Returns400FieldErrors_BeforeLookingAtTheCart()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.CheckoutAsync(new
        {
            shippingName = "Test Customer",
            shippingPhone = "+20abc",
            city = (string?)null,
            addressLine = "12 Tahrir Street",
            notes = new string('n', 501)
        });

        // The cart is empty, so field errors (not cartEmpty) prove validation runs first.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().Keys.ShouldBe(["shippingPhone", "city", "notes"], ignoreOrder: true);
    }

    [Fact]
    public async Task EmptyBody_ReturnsAllRequiredFieldErrors()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.CheckoutAsync(new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys
            .ShouldBe(["shippingName", "shippingPhone", "city", "addressLine"], ignoreOrder: true);
    }

    [Fact]
    public async Task NullBody_Returns400()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.PostAsync(
            "/api/orders",
            new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Code.ShouldBe("validationFailed");
    }

    [Fact]
    public async Task ShortValuesAfterTrimming_AreRejected()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var response = await client.CheckoutAsync(new
        {
            shippingName = "  A  ",
            shippingPhone = "  ",
            city = " C ",
            addressLine = "  12  ",
            notes = (string?)null
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().Keys
            .ShouldBe(["shippingName", "shippingPhone", "city", "addressLine"], ignoreOrder: true);
    }

    [Fact]
    public async Task DeactivatedProduct_Returns409CartChanged_AndChangesNothing()
    {
        var (client, auth) = await factory.NewCustomerAsync();
        var cart = await (await client.AddToCartAsync(await factory.VariantIdAsync(SatinBlouseWhiteS), 1)).ReadCartAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1);
        var blouseItemId = cart.Items[0].ItemId;
        var ribbedStock = await factory.StockAsync(RibbedTopWhiteS);
        var blouseStock = await factory.StockAsync(SatinBlouseWhiteS);

        await using (var context = factory.CreateDbContext())
        {
            await context.Products.Where(product => product.Slug == "satin-blouse")
                .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.IsActive, false));
        }

        var response = await client.CheckoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorAsync()).Code.ShouldBe("cartChanged");
        var changed = (await response.ReadErrorDetailsAsync()).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        changed.GetProperty("itemId").GetInt32().ShouldBe(blouseItemId);
        changed.GetProperty("status").GetString().ShouldBe("unavailable");

        (await factory.StockAsync(RibbedTopWhiteS)).ShouldBe(ribbedStock);
        (await factory.StockAsync(SatinBlouseWhiteS)).ShouldBe(blouseStock);
        (await client.GetCartAsync()).ItemCount.ShouldBe(2);
        await using var check = factory.CreateDbContext();
        (await check.Orders.AnyAsync(order => order.UserId == auth.User.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task StockBelowQuantity_Returns409CartChanged_WithMaxQuantity()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantId = await factory.VariantIdAsync(LinenShirtBeigeM);
        await client.AddToCartAsync(variantId, 3);
        await SetStockAsync(LinenShirtBeigeM, 1);

        var response = await client.CheckoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var changed = (await response.ReadErrorDetailsAsync()).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        changed.GetProperty("variantId").GetInt32().ShouldBe(variantId);
        changed.GetProperty("status").GetString().ShouldBe("quantityReduced");
        changed.GetProperty("maxQuantity").GetInt32().ShouldBe(1);
        (await factory.StockAsync(LinenShirtBeigeM)).ShouldBe(1);
    }

    [Fact]
    public async Task StockZero_Returns409CartChanged_OutOfStock()
    {
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(GraphicTeeGrayL), 1);
        await SetStockAsync(GraphicTeeGrayL, 0);

        var response = await client.CheckoutAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var changed = (await response.ReadErrorDetailsAsync()).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        changed.GetProperty("status").GetString().ShouldBe("outOfStock");
        changed.GetProperty("maxQuantity").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ShippingDetailsAndNotes_AreStoredTrimmed()
    {
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(ChinosNavyM), 1);

        var response = await client.CheckoutAsync(new
        {
            shippingName = "  Mona Ali ",
            shippingPhone = " 01000000003 ",
            city = " Giza ",
            addressLine = "  5 Pyramids Road  ",
            notes = "  Call before delivery  "
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.ReadOrderAsync();
        order.ShippingName.ShouldBe("Mona Ali");
        order.ShippingPhone.ShouldBe("01000000003");
        order.City.ShouldBe("Giza");
        order.AddressLine.ShouldBe("5 Pyramids Road");
        order.Notes.ShouldBe("Call before delivery");

        await using var context = factory.CreateDbContext();
        (await context.Orders.Where(saved => saved.Id == order.Id).Select(saved => saved.Notes).SingleAsync())
            .ShouldBe("Call before delivery");
    }

    [Fact]
    public async Task BlankNotes_AreStoredAsNull()
    {
        var (client, _) = await factory.NewCustomerAsync();
        await client.AddToCartAsync(await factory.VariantIdAsync(RibbedTopWhiteS), 1);

        var response = await client.CheckoutAsync(new
        {
            shippingName = "Test Customer",
            shippingPhone = "+201000000000",
            city = "Cairo",
            addressLine = "12 Tahrir Street",
            notes = "   "
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadOrderAsync()).Notes.ShouldBeNull();
    }

    [Fact]
    public async Task TwoCheckouts_GetIncreasingOrderNumbers()
    {
        var (client, _) = await factory.NewCustomerAsync();
        var variantId = await factory.VariantIdAsync(RibbedTopWhiteS);

        await client.AddToCartAsync(variantId, 1);
        var first = await (await client.CheckoutAsync()).ReadOrderAsync();
        await client.AddToCartAsync(variantId, 1);
        var second = await (await client.CheckoutAsync()).ReadOrderAsync();

        OrderRecords.NumberOf(second.OrderNumber).ShouldBeGreaterThan(OrderRecords.NumberOf(first.OrderNumber));
        second.Id.ShouldBeGreaterThan(first.Id);
    }

    private async Task SetStockAsync(string sku, int stock)
    {
        await using var context = factory.CreateDbContext();
        await context.ProductVariants.Where(variant => variant.Sku == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(variant => variant.Stock, stock));
    }
}
