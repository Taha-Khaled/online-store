using Microsoft.AspNetCore.Mvc;
using Store.Application.Common;
using Store.Application.Products.Requests;
using Store.Application.Products.Responses;
using Store.Application.Products.Services;

namespace Store.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    /// <summary>Product list; <c>color</c> and <c>size</c> may be repeated.</summary>
    [HttpGet]
    public Task<PagedResponse<ProductListItemResponse>> List([FromQuery] ProductListQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);

    /// <summary>Filter panel options, optionally limited to a category slug (literal route wins over a slug).</summary>
    [HttpGet("filters")]
    public Task<ProductFiltersResponse> Filters([FromQuery] string? category, CancellationToken cancellationToken) =>
        products.GetFiltersAsync(category, cancellationToken);

    [HttpGet("{slug}")]
    public Task<ProductDetailsResponse> GetBySlug(string slug, CancellationToken cancellationToken) =>
        products.GetBySlugAsync(slug, cancellationToken);
}
