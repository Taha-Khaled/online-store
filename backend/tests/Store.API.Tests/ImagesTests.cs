using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US1-9 / AC 11: product images are public static files (FR-007, research R11).</summary>
public class ImagesTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SeedImage_IsServedWithoutToken()
    {
        var response = await _client.GetAsync("/images/products/classic-cotton-t-shirt-black.jpg");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MissingImage_Returns404NotFound()
    {
        var response = await _client.GetAsync("/images/products/does-not-exist.jpg");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("notFound");
    }

    [Theory]
    [InlineData("/images/products/")]
    [InlineData("/images/products/%2E%2E/appsettings.json")]
    [InlineData("/images/products/..%2Fappsettings.json")]
    public async Task DirectoryAndPathsOutsideTheFolder_AreNotServed(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("ConnectionStrings");
    }
}
