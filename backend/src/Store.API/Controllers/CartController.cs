using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.API.Authorization;
using Store.Application.Cart.Requests;
using Store.Application.Cart.Responses;
using Store.Application.Cart.Services;

namespace Store.API.Controllers;

/// <summary>The logged-in customer's server cart (FR-003–FR-011). Customers only.</summary>
[ApiController]
[Route("api/cart")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class CartController(CartReader reader, CartService cart) : ControllerBase
{
    [HttpGet]
    public Task<CartResponse> Get(CancellationToken cancellationToken) =>
        reader.GetAsync(User.GetUserId(), cancellationToken);

    [HttpPost("items")]
    public Task<CartResponse> AddItem([FromBody] AddCartItemRequest request, CancellationToken cancellationToken) =>
        cart.AddItemAsync(User.GetUserId(), request, cancellationToken);

    [HttpPut("items/{itemId:int}")]
    public Task<CartResponse> UpdateItem(int itemId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken) =>
        cart.UpdateItemAsync(User.GetUserId(), itemId, request, cancellationToken);

    [HttpDelete("items/{itemId:int}")]
    public Task<CartResponse> RemoveItem(int itemId, CancellationToken cancellationToken) =>
        cart.RemoveItemAsync(User.GetUserId(), itemId, cancellationToken);

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cart.ClearAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
