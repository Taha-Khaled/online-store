using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>AC 18 / SC-003: paging never skips or repeats a product in any sort order (FR-036, FR-037).</summary>
public class ProductPagingTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private const int PageSize = 5;

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("newest")]
    [InlineData("priceAsc")]
    [InlineData("priceDesc")]
    [InlineData("nameAsc")]
    public async Task PagingWithSize5_ReturnsEveryProductExactlyOnce(string sort)
    {
        var paged = new List<int>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await GetPageAsync($"/api/products?sort={sort}&pageSize={PageSize}&page={page}");
            result.TotalCount.ShouldBe(18);
            result.TotalPages.ShouldBe(4);
            paged.AddRange(result.Items.Select(item => item.Id));
        }

        var single = await GetPageAsync($"/api/products?sort={sort}&pageSize=48");

        paged.Count.ShouldBe(18);
        paged.ShouldBeUnique();
        paged.ShouldBe(single.Items.Select(item => item.Id));
    }

    private async Task<ProductPage> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadJsonAsync<ProductPage>();
    }
}
