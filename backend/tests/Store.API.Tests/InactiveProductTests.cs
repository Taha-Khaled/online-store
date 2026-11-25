using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>
/// US1-10, US2-2 / AC 9: a product deactivated directly in the database disappears from every public
/// endpoint (FR-030). Own class, so its own database: the change does not affect other tests.
/// </summary>
public class InactiveProductTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>, IAsyncLifetime
{
    private const string Slug = "leather-derby-shoes";

    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        // CreateClient started the app, so the database is migrated and seeded.
        await using var context = factory.CreateDbContext();
        var updated = await context.Products
            .Where(product => product.Slug == Slug)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.IsActive, false));
        updated.ShouldBe(1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeactivatedProduct_IsMissingFromTheList()
    {
        var page = await GetJsonAsync<ProductPage>("/api/products?pageSize=48");

        page.TotalCount.ShouldBe(17);
        page.Items.Select(item => item.Slug).ShouldNotContain(Slug);
    }

    [Fact]
    public async Task DeactivatedProduct_IsNotFoundBySearchOrCategory()
    {
        var search = await GetJsonAsync<ProductPage>("/api/products?search=derby");
        var formal = await GetJsonAsync<ProductPage>("/api/products?category=shoes-formal");

        search.TotalCount.ShouldBe(0);
        formal.Items.ShouldHaveSingleItem().Name.ShouldBe("Classic Loafers");
    }

    [Fact]
    public async Task DeactivatedProduct_DetailsReturn404ProductNotFound()
    {
        var response = await _client.GetAsync($"/api/products/{Slug}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("productNotFound");
    }

    [Fact]
    public async Task DeactivatedProduct_NoLongerCountsInTheFilterPanel()
    {
        var panel = await GetJsonAsync<FilterPanel>("/api/products/filters");

        // Leather Derby Shoes (2200) was the most expensive product; Running Shoes (1900) is next.
        panel.MaxPrice.ShouldBe(1900m);
    }

    private async Task<T> GetJsonAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadJsonAsync<T>();
    }
}
