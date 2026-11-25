using Microsoft.AspNetCore.Mvc;
using Store.Application.Common;
using Store.Application.Products;

namespace Store.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    /// <summary>Product list; <c>color</c> and <c>size</c> may be repeated.</summary>
    [HttpGet]
    public Task<PagedResponse<ProductListItemResponse>> List([FromQuery] ProductListQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);
}
