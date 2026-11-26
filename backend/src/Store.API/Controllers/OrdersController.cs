using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.API.Authorization;
using Store.Application.Orders.Requests;
using Store.Application.Orders.Responses;
using Store.Application.Orders.Services;

namespace Store.API.Controllers;

/// <summary>Checkout and the logged-in customer's orders (FR-014–FR-024). Customers only.</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class OrdersController(CheckoutService checkout) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDetailsResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await checkout.CheckoutAsync(User.GetUserId(), request, cancellationToken);
        return Created($"/api/orders/{order.Id}", order);
    }
}
