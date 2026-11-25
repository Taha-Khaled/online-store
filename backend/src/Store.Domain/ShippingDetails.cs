namespace Store.Domain;

/// <summary>Where and to whom a new order is delivered (cash on delivery).</summary>
public sealed record ShippingDetails(string Name, string Phone, string City, string AddressLine, string? Notes);
