namespace Store.Application.Cart.Requests;

/// <summary>The guest cart sent after login (FR-012, contracts/api.md "POST /api/cart/merge").</summary>
public sealed record MergeCartRequest(IReadOnlyList<MergeCartItem> Items);

public sealed record MergeCartItem(int VariantId, int Quantity);
