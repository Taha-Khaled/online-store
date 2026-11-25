using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US1-1 / AC 4: the public category tree from the 001 seed.</summary>
public class CategoriesTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    private sealed record Child(string Name, string Slug);

    private sealed record Parent(string Name, string Slug, List<Child> Children);

    [Fact]
    public async Task GetCategories_ReturnsThreeParentsWithThreeChildrenEach()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tree = await response.ReadJsonAsync<List<Parent>>();
        tree.Select(Describe).ShouldBe(
        [
            "Men/men: T-Shirts/men-t-shirts, Shirts/men-shirts, Pants/men-pants",
            "Women/women: Dresses/women-dresses, Tops/women-tops, Pants/women-pants",
            "Shoes/shoes: Sneakers/shoes-sneakers, Formal/shoes-formal, Sandals/shoes-sandals"
        ]);
    }

    [Fact]
    public async Task GetCategories_WorksWithoutToken()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    private static string Describe(Parent parent) =>
        $"{parent.Name}/{parent.Slug}: " + string.Join(", ", parent.Children.Select(child => $"{child.Name}/{child.Slug}"));
}
