using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US1 / AC 5, 6, 7, 10: product list defaults, filters, sorting, paging and validation against the
/// 001 seed (18 active products, all created at the same time).
/// </summary>
public class ProductListTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private static readonly string[] FeaturedNames =
    [
        "Classic Cotton T-Shirt", "Oxford Button-Down Shirt", "Classic Denim Jeans",
        "Floral Midi Dress", "Wide Leg Trousers", "Everyday White Sneakers"
    ];

    private static readonly string[] ShoeNames =
    [
        "Everyday White Sneakers", "Running Shoes", "Leather Derby Shoes",
        "Classic Loafers", "Leather Sandals", "Everyday Slides"
    ];

    private static readonly string[] NameOrder =
    [
        "Basic Ribbed Top", "Casual Shirt Dress", "Classic Cotton T-Shirt", "Classic Denim Jeans",
        "Classic Loafers", "Everyday Slides", "Everyday White Sneakers", "Floral Midi Dress",
        "Graphic Oversized Tee", "High Waist Jeans", "Leather Derby Shoes", "Leather Sandals",
        "Linen Summer Shirt", "Oxford Button-Down Shirt", "Running Shoes", "Satin Blouse",
        "Slim Fit Chinos", "Wide Leg Trousers"
    ];

    private static readonly string[] PriceAscOrder =
    [
        "Basic Ribbed Top", "Classic Cotton T-Shirt", "Everyday Slides", "Graphic Oversized Tee",
        "Satin Blouse", "Leather Sandals", "Oxford Button-Down Shirt", "Linen Summer Shirt",
        "Slim Fit Chinos", "Wide Leg Trousers", "Casual Shirt Dress", "High Waist Jeans",
        "Classic Denim Jeans", "Floral Midi Dress", "Everyday White Sneakers", "Classic Loafers",
        "Running Shoes", "Leather Derby Shoes"
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Defaults_Return18Products_12Then6_NewestFirst()
    {
        var page1 = await GetPageAsync("/api/products");
        var page2 = await GetPageAsync("/api/products?page=2");

        page1.TotalCount.ShouldBe(18);
        page1.TotalPages.ShouldBe(2);
        page1.Page.ShouldBe(1);
        page1.PageSize.ShouldBe(12);
        page1.Items.Count.ShouldBe(12);
        page2.Items.Count.ShouldBe(6);
        // Same creation date for every seeded product, so newest falls back to id descending.
        page1.Items.Concat(page2.Items).Select(item => item.Id).ShouldBe(Enumerable.Range(1, 18).Reverse());
    }

    [Fact]
    public async Task PageBeyondLast_ReturnsEmptyListWithTotals()
    {
        var page = await GetPageAsync("/api/products?page=5");

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(18);
        page.TotalPages.ShouldBe(2);
        page.Page.ShouldBe(5);
    }

    [Fact]
    public async Task Featured_ReturnsTheSixFeaturedProducts()
    {
        var page = await GetPageAsync("/api/products?featured=true");

        page.TotalCount.ShouldBe(6);
        page.Items.Select(item => item.Name).ShouldBe(FeaturedNames, ignoreOrder: true);
        page.Items.ShouldAllBe(item => item.IsFeatured);
    }

    [Theory]
    [InlineData("shoes", 6)]
    [InlineData("shoes-sneakers", 2)]
    [InlineData("women", 6)]
    [InlineData("men-pants", 2)]
    public async Task Category_IncludesChildCategories(string slug, int expected)
    {
        var page = await GetPageAsync($"/api/products?category={slug}");

        page.TotalCount.ShouldBe(expected);
    }

    [Fact]
    public async Task CategoryShoes_ReturnsTheShoeProducts()
    {
        var page = await GetPageAsync("/api/products?category=shoes");

        page.Items.Select(item => item.Name).ShouldBe(ShoeNames, ignoreOrder: true);
    }

    [Fact]
    public async Task UnknownCategory_Returns404CategoryNotFound()
    {
        var response = await _client.GetAsync("/api/products?category=sneakers");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("categoryNotFound");
    }

    [Fact]
    public async Task SortPriceAsc_StartsWithBasicRibbedTop_AndBreaksTiesByName()
    {
        var page = await GetPageAsync("/api/products?sort=priceAsc&pageSize=48");

        page.Items.Select(item => item.Name).ShouldBe(PriceAscOrder);
        page.Items[0].Price.ShouldBe(300m);
    }

    [Fact]
    public async Task SortPriceDesc_StartsWithLeatherDerbyShoes()
    {
        var page = await GetPageAsync("/api/products?sort=priceDesc");

        page.Items[0].Name.ShouldBe("Leather Derby Shoes");
        page.Items[0].Price.ShouldBe(2200m);
        page.Items.Select(item => item.Price).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task SortNameAsc_OrdersByName()
    {
        var page = await GetPageAsync("/api/products?sort=nameAsc&pageSize=48");

        page.Items.Select(item => item.Name).ShouldBe(NameOrder);
    }

    [Fact]
    public async Task SortName_IsCaseInsensitive()
    {
        var page = await GetPageAsync("/api/products?sort=PRICEASC");

        page.Items[0].Name.ShouldBe("Basic Ribbed Top");
    }

    [Fact]
    public async Task ColorAndSizeNames_AreCaseInsensitive()
    {
        var lower = await GetPageAsync("/api/products?color=black&size=xl&pageSize=48");
        var upper = await GetPageAsync("/api/products?color=BLACK&size=XL&pageSize=48");

        lower.TotalCount.ShouldBeGreaterThan(0);
        upper.Items.Select(item => item.Id).ShouldBe(lower.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task SeveralColors_MatchAnyOfThem()
    {
        var page = await GetPageAsync("/api/products?color=red&color=brown");

        page.Items.Select(item => item.Name).ShouldBe(
            ["Floral Midi Dress", "Satin Blouse", "Leather Derby Shoes", "Classic Loafers", "Leather Sandals"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ColorAndSize_MustMatchTheSameInStockVariant()
    {
        // Classic Cotton T-Shirt has White (other sizes) and L (other colors) in stock, but White/L is sold out.
        var colorOnly = await GetPageAsync("/api/products?color=white&pageSize=48");
        var sizeOnly = await GetPageAsync("/api/products?size=l&pageSize=48");
        var both = await GetPageAsync("/api/products?color=white&size=l&pageSize=48");

        colorOnly.Items.Select(item => item.Name).ShouldContain("Classic Cotton T-Shirt");
        sizeOnly.Items.Select(item => item.Name).ShouldContain("Classic Cotton T-Shirt");
        both.Items.Select(item => item.Name).ShouldNotContain("Classic Cotton T-Shirt");
        both.Items.Select(item => item.Name).ShouldContain("Oxford Button-Down Shirt");
    }

    [Fact]
    public async Task UnknownColor_MatchesNothing()
    {
        var page = await GetPageAsync("/api/products?color=purple");

        page.TotalCount.ShouldBe(0);
        page.TotalPages.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PriceRange_IsInclusive()
    {
        var page = await GetPageAsync("/api/products?minPrice=1050&maxPrice=1250");

        page.Items.Select(item => item.Name).ShouldBe(
            ["Casual Shirt Dress", "High Waist Jeans", "Classic Denim Jeans", "Floral Midi Dress"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ListItem_HasContractFields()
    {
        var page = await GetPageAsync("/api/products?search=classic%20cotton");

        var item = page.Items.ShouldHaveSingleItem();
        item.Slug.ShouldBe("classic-cotton-t-shirt");
        item.Price.ShouldBe(350m);
        item.CategoryName.ShouldBe("T-Shirts");
        item.IsFeatured.ShouldBeTrue();
        item.InStock.ShouldBeTrue();
        // Main image = first color in the seed's color list; colors follow their image order.
        item.MainImageUrl.ShouldBe("/images/products/classic-cotton-t-shirt-black.jpg");
        item.Colors.Select(color => color.Name).ShouldBe(["Black", "White", "Navy"]);
        item.Colors.ShouldAllBe(color => color.HexCode.StartsWith('#'));
    }

    [Theory]
    [InlineData("sort=random", "sort")]
    [InlineData("sort=1", "sort")]
    [InlineData("pageSize=49", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("minPrice=500&maxPrice=100", "minPrice")]
    [InlineData("minPrice=-1", "minPrice")]
    [InlineData("maxPrice=-1", "maxPrice")]
    [InlineData("page=abc", "page")]
    public async Task InvalidOptions_Return400WithFieldError(string queryString, string field)
    {
        var response = await _client.GetAsync($"/api/products?{queryString}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadErrorAsync();
        error.Code.ShouldBe("validationFailed");
        error.Errors.ShouldNotBeNull().ShouldContainKey(field);
    }

    [Fact]
    public async Task SearchLongerThan100Characters_Returns400()
    {
        var response = await _client.GetAsync($"/api/products?search={new string('a', 101)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey("search");
    }

    [Fact]
    public async Task SearchOf100Characters_IsAccepted()
    {
        var response = await _client.GetAsync($"/api/products?search={new string('a', 100)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ProductPage> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<ProductPage>();
    }
}
