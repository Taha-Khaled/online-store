using System.Text.Json.Serialization;
using Store.Domain.Enums;

namespace Store.Application.Orders.Responses;

/// <summary>One row of the customer's order list. <see cref="ItemCount"/> is the number of order lines.</summary>
public sealed record OrderSummaryResponse(
    int Id,
    string OrderNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))] OrderStatus Status,
    int ItemCount,
    decimal Total,
    DateTime CreatedAt);
