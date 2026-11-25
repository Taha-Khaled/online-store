namespace Store.Application.Categories;

public sealed record CategoryResponse(string Name, string Slug, IReadOnlyList<CategoryChildResponse> Children);

public sealed record CategoryChildResponse(string Name, string Slug);
