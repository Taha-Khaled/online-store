using System.Text.Json.Serialization;

namespace Store.Application.Cart.Responses;

/// <summary>Why a guest cart entry was skipped or reduced during the merge. Serialized in camelCase.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MergeAdjustmentReason>))]
public enum MergeAdjustmentReason
{
    /// <summary>Unknown variant or inactive product; skipped.</summary>
    [JsonStringEnumMemberName("variantNotFound")]
    VariantNotFound,

    /// <summary>Stock is 0; skipped.</summary>
    [JsonStringEnumMemberName("outOfStock")]
    OutOfStock,

    /// <summary>The cart already holds the maximum number of lines; skipped.</summary>
    [JsonStringEnumMemberName("cartFull")]
    CartFull,

    /// <summary>Stock is below the merged quantity; the line was set to the stock.</summary>
    [JsonStringEnumMemberName("quantityReduced")]
    QuantityReduced
}
