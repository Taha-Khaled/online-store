namespace Store.Application.Orders.Requests;

/// <summary>Shipping details for a cash-on-delivery checkout (FR-014).</summary>
public sealed record PlaceOrderRequest(
    string ShippingName,
    string ShippingPhone,
    string City,
    string AddressLine,
    string? Notes);
