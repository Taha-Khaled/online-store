using Microsoft.AspNetCore.Mvc;
using Store.Application.Categories.Responses;
using Store.Application.Categories.Services;

namespace Store.API.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryResponse>> GetTree(CancellationToken cancellationToken) =>
        categories.GetTreeAsync(cancellationToken);
}
