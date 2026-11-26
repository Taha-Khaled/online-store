using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.API.Authorization;
using Store.Application.Common;
using Store.Application.Orders.Requests;
using Store.Application.Orders.Responses;
using Store.Application.Orders.Services;

namespace Store.API.Controllers;

/// <summary>Checkout and the logged-in customer's orders (FR-014–FR-024). Customers only.</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class OrdersController(CheckoutService checkout, CustomerOrderService orders) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDetailsResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await checkout.CheckoutAsync(User.GetUserId(), request, cancellationToken);
        return Created($"/api/orders/{order.Id}", order);
    }

    [HttpGet]
    public Task<PagedResponse<OrderSummaryResponse>> List([FromQuery] OrderListQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(User.GetUserId(), query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<OrderDetailsResponse> Get(int id, CancellationToken cancellationToken) =>
        orders.GetAsync(User.GetUserId(), id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    public Task<OrderDetailsResponse> Cancel(int id, CancellationToken cancellationToken) =>
        orders.CancelAsync(User.GetUserId(), id, cancellationToken);
}
