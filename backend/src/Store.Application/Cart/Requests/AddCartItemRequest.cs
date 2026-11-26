namespace Store.Application.Cart.Requests;

public sealed record AddCartItemRequest(int VariantId, int Quantity);
