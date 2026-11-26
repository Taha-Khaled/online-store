using System.Linq.Expressions;
using Store.Application.Orders.Responses;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Application.Orders.Services;

/// <summary>
/// Order → <see cref="OrderDetailsResponse"/> from the stored snapshots only (research R12). One
/// definition serves both SQL projections (<see cref="ToDetailsExpression"/>) and orders already in
/// memory (<see cref="ToDetails"/>). A customer may cancel only a Pending order.
/// </summary>
public static class OrderMapping
{
    public static readonly Expression<Func<Order, OrderDetailsResponse>> ToDetailsExpression = order =>
        new OrderDetailsResponse(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.CreatedAt,
            order.UpdatedAt,
            order.ShippingName,
            order.ShippingPhone,
            order.City,
            order.AddressLine,
            order.Notes,
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResponse(
                    item.ProductVariantId,
                    item.ProductName,
                    item.ColorName,
                    item.SizeName,
                    item.UnitPrice,
                    item.Quantity,
                    item.UnitPrice * item.Quantity))
                .ToList(),
            order.Subtotal,
            order.ShippingFee,
            order.Total,
            order.Status == OrderStatus.Pending);

    private static readonly Func<Order, OrderDetailsResponse> Compiled = ToDetailsExpression.Compile();

    public static OrderDetailsResponse ToDetails(Order order) => Compiled(order);
}
