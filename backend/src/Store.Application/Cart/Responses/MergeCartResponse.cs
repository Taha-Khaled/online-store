namespace Store.Application.Cart.Responses;

/// <summary>The merged cart plus every guest entry that was skipped or reduced.</summary>
public sealed record MergeCartResponse(CartResponse Cart, IReadOnlyList<MergeAdjustmentResponse> Adjustments);

/// <summary>
/// <see cref="RequestedQuantity"/> is the guest quantity for a skipped entry, and for a reduced one
/// the quantity the merge aimed for (the higher of the cart line and the guest entry).
/// <see cref="Quantity"/> is what was set, 0 for a skipped entry.
/// </summary>
public sealed record MergeAdjustmentResponse(
    int VariantId,
    MergeAdjustmentReason Reason,
    int RequestedQuantity,
    int Quantity);
