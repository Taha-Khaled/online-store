using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US1-5/6/7, AC 16–17: text search (FR-032) and its combination with filters (FR-034).</summary>
public class ProductSearchTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private static readonly string[] ShoeNames =
    [
        "Everyday White Sneakers", "Running Shoes", "Leather Derby Shoes",
        "Classic Loafers", "Leather Sandals", "Everyday Slides"
    ];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SearchShoes_FindsAllShoeProductsThroughTheirCategory()
    {
        var page = await GetPageAsync("/api/products?search=shoes");

        // Sneakers, loafers, sandals and slides do not contain "shoes" in name or description.
        page.Items.Select(item => item.Name).ShouldBe(ShoeNames, ignoreOrder: true);
    }

    [Fact]
    public async Task SearchShoesWithSize42_ExcludesSneakersWhoseSize42IsSoldOut()
    {
        var page = await GetPageAsync("/api/products?search=shoes&size=42");

        page.TotalCount.ShouldBe(5);
        page.Items.Select(item => item.Name).ShouldNotContain("Everyday White Sneakers");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var upper = await GetPageAsync("/api/products?search=JEANS");
        var lower = await GetPageAsync("/api/products?search=jeans");

        lower.Items.Select(item => item.Name).ShouldBe(["High Waist Jeans", "Classic Denim Jeans"]);
        upper.Items.Select(item => item.Id).ShouldBe(lower.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Search_MatchesPartOfTheDescription()
    {
        var page = await GetPageAsync("/api/products?search=BREATHABLE");

        page.Items.ShouldHaveSingleItem().Name.ShouldBe("Classic Cotton T-Shirt");
    }

    [Theory]
    [InlineData("%25")]
    [InlineData("_")]
    [InlineData("%5B")]
    [InlineData("%5Ba-z%5D")]
    public async Task WildcardCharacters_AreMatchedLiterally(string encodedSearch)
    {
        var page = await GetPageAsync($"/api/products?search={encodedSearch}");

        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task WhitespaceSearch_IsIgnored()
    {
        var page = await GetPageAsync("/api/products?search=%20%20%20");

        page.TotalCount.ShouldBe(18);
    }

    [Fact]
    public async Task SearchAndFilters_MustAllMatch()
    {
        var jeansBlue = await GetPageAsync("/api/products?search=jeans&color=blue");
        var jeansBlueXxl = await GetPageAsync("/api/products?search=jeans&color=blue&size=xxl");
        var jeansCheap = await GetPageAsync("/api/products?search=jeans&maxPrice=1060");

        jeansBlue.TotalCount.ShouldBe(2);
        jeansBlueXxl.Items.ShouldHaveSingleItem().Name.ShouldBe("Classic Denim Jeans");
        jeansCheap.Items.ShouldHaveSingleItem().Name.ShouldBe("High Waist Jeans");
    }

    private async Task<ProductPage> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<ProductPage>();
    }
}
