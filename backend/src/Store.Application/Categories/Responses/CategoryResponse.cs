namespace Store.Application.Categories.Responses;

public sealed record CategoryResponse(string Name, string Slug, IReadOnlyList<CategoryChildResponse> Children);

public sealed record CategoryChildResponse(string Name, string Slug);
