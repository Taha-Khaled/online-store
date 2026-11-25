using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>FR-041: filter panel options from in-stock variants of active products.</summary>
public class FilterPanelTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private static readonly string[] ShoeSizes = ["38", "39", "40", "41", "42", "43", "44", "45"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task WholeCatalog_ReturnsAllColorsBySizeOrderAndPriceRange()
    {
        var panel = await GetPanelAsync("/api/products/filters");

        panel.Colors.Select(color => color.Name).ShouldBe(["Beige", "Black", "Blue", "Brown", "Gray", "Navy", "Red", "White"]);
        panel.Sizes.Select(size => size.Name).ShouldBe(["XS", "S", "M", "L", "XL", "XXL", .. ShoeSizes]);
        panel.MinPrice.ShouldBe(300m);
        panel.MaxPrice.ShouldBe(2200m);
    }

    [Fact]
    public async Task ShoesCategory_ReturnsOnlyShoeOptions()
    {
        var panel = await GetPanelAsync("/api/products/filters?category=shoes");

        panel.Colors.Select(color => color.Name).ShouldBe(["Black", "Brown", "Gray", "White"]);
        panel.Sizes.Select(size => size.Name).ShouldBe(ShoeSizes);
        panel.MinPrice.ShouldBe(400m);
        panel.MaxPrice.ShouldBe(2200m);
    }

    [Fact]
    public async Task UnknownCategory_Returns404CategoryNotFound()
    {
        var response = await _client.GetAsync("/api/products/filters?category=hats");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("categoryNotFound");
    }

    private async Task<FilterPanel> GetPanelAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<FilterPanel>();
    }
}
