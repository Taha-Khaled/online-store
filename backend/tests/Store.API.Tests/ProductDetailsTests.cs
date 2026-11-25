using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US2 / AC 8: product details with availability but never exact stock (FR-040, SC-008).</summary>
public class ProductDetailsTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private sealed record Category(string Name, string Slug, string? ParentName);

    private sealed record Image(string Url, bool IsMain);

    private sealed record Color(int Id, string Name, string HexCode, List<Image> Images);

    private sealed record Size(int Id, string Name);

    private sealed record Variant(int Id, int ColorId, int SizeId, string Sku, bool InStock, int MaxQuantity);

    private sealed record Details(
        int Id,
        string Name,
        string Slug,
        string Description,
        decimal Price,
        Category Category,
        List<Color> Colors,
        List<Size> Sizes,
        List<Variant> Variants);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ClassicCottonTShirt_ShowsColorsSizesAndVariantAvailability()
    {
        var details = await GetDetailsAsync("classic-cotton-t-shirt");

        details.Name.ShouldBe("Classic Cotton T-Shirt");
        details.Price.ShouldBe(350m);
        details.Description.ShouldNotBeNullOrWhiteSpace();
        details.Category.ShouldBe(new Category("T-Shirts", "men-t-shirts", "Men"));
        details.Colors.Select(color => color.Name).ShouldBe(["Black", "White", "Navy"]);
        details.Sizes.Select(size => size.Name).ShouldBe(["S", "M", "L", "XL"]);
        details.Variants.Count.ShouldBe(12);

        var whiteL = Find(details, "White", "L");
        whiteL.InStock.ShouldBeFalse();
        whiteL.MaxQuantity.ShouldBe(0);
        whiteL.Sku.ShouldBe("MTS-001-WHT-L");

        var blackM = Find(details, "Black", "M");
        blackM.InStock.ShouldBeTrue();
        blackM.MaxQuantity.ShouldBe(1);

        // Every other variant has seeded stock 8–20, capped at 5 per order.
        details.Variants.Where(variant => variant != whiteL && variant != blackM)
            .ShouldAllBe(variant => variant.InStock && variant.MaxQuantity == 5);
    }

    [Fact]
    public async Task Colors_CarryTheirImagesInDisplayOrder()
    {
        var details = await GetDetailsAsync("classic-cotton-t-shirt");

        details.Colors.Single(color => color.Name == "Black").Images
            .ShouldBe([new Image("/images/products/classic-cotton-t-shirt-black.jpg", true)]);
        details.Colors.ShouldAllBe(color => color.HexCode.StartsWith('#') && color.Images.Count == 1);
    }

    [Fact]
    public async Task ShoeProduct_ListsShoeSizesInDisplayOrderAndLowStockQuantity()
    {
        var details = await GetDetailsAsync("leather-derby-shoes");

        details.Category.ParentName.ShouldBe("Shoes");
        details.Sizes.Select(size => size.Name).ShouldBe(["40", "41", "42", "43", "44", "45"]);
        Find(details, "Brown", "44").MaxQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task Response_NeverContainsStock()
    {
        var response = await _client.GetAsync("/api/products/classic-cotton-t-shirt");

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotContain("\"stock\"", Case.Sensitive);
        json.ShouldContain("\"inStock\"", Case.Sensitive);
    }

    [Fact]
    public async Task Slug_IsCaseInsensitive()
    {
        var details = await GetDetailsAsync("CLASSIC-COTTON-T-SHIRT");

        details.Slug.ShouldBe("classic-cotton-t-shirt");
    }

    [Fact]
    public async Task UnknownSlug_Returns404ProductNotFound()
    {
        var response = await _client.GetAsync("/api/products/no-such-product");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("productNotFound");
        error.Message.ShouldBe("Product not found.");
    }

    private static Variant Find(Details details, string color, string size)
    {
        var colorId = details.Colors.Single(candidate => candidate.Name == color).Id;
        var sizeId = details.Sizes.Single(candidate => candidate.Name == size).Id;
        return details.Variants.Single(variant => variant.ColorId == colorId && variant.SizeId == sizeId);
    }

    private async Task<Details> GetDetailsAsync(string slug)
    {
        var response = await _client.GetAsync($"/api/products/{slug}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<Details>();
    }
}
