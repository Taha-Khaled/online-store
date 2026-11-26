using System.Text.Json.Serialization;
using Store.Domain.Enums;

namespace Store.Application.Orders.Responses;

/// <summary>One order of the customer (contracts/api.md "OrderDetails"). Status is written as its name.</summary>
public sealed record OrderDetailsResponse(
    int Id,
    string OrderNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))] OrderStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ShippingName,
    string ShippingPhone,
    string City,
    string AddressLine,
    string? Notes,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    bool CanCancel);
