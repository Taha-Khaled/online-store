using System.Text.Json.Serialization;

namespace Store.Application.Cart.Responses;

/// <summary>Availability of a cart line against current data (FR-003). Serialized in camelCase.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CartItemStatus>))]
public enum CartItemStatus
{
    [JsonStringEnumMemberName("available")]
    Available,

    /// <summary>Stock is above 0 but below the cart quantity.</summary>
    [JsonStringEnumMemberName("quantityReduced")]
    QuantityReduced,

    [JsonStringEnumMemberName("outOfStock")]
    OutOfStock,

    /// <summary>The product is no longer active.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable
}
