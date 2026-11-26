using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Store.Application.Common;
using Store.Domain;

namespace Store.Application.Cart;

/// <summary>
/// Builds the cart response with one read-only projection (FR-003–FR-005, research R7): current
/// price, the main image for the line's color, and the status and purchasable quantity computed in
/// SQL, so the exact stock never leaves the database.
/// </summary>
public sealed class CartReader(IAppDbContext db, IOptions<ShopOptions> shopOptions)
{
    public async Task<CartResponse> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var items = await db.CartItems
            .AsNoTracking()
            .Where(line => line.UserId == userId)
            .OrderBy(line => line.AddedAt)
            .ThenBy(line => line.Id)
            .Select(line => new CartItemResponse(
                line.Id,
                line.ProductVariantId,
                line.ProductVariant.Product.Name,
                line.ProductVariant.Product.Slug,
                line.ProductVariant.Color.Name,
                line.ProductVariant.Size.Name,
                line.ProductVariant.Product.Price,
                line.ProductVariant.Product.Price * line.Quantity,
                line.ProductVariant.Product.Images
                    .Where(image => image.IsMain && image.ColorId == line.ProductVariant.ColorId)
                    .OrderBy(image => image.DisplayOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                line.Quantity,
                line.ProductVariant.Stock < StoreLimits.MaxQuantityPerVariant
                    ? line.ProductVariant.Stock
                    : StoreLimits.MaxQuantityPerVariant,
                !line.ProductVariant.Product.IsActive ? CartItemStatus.Unavailable
                : line.ProductVariant.Stock == 0 ? CartItemStatus.OutOfStock
                : line.ProductVariant.Stock < line.Quantity ? CartItemStatus.QuantityReduced
                : CartItemStatus.Available))
            .ToListAsync(cancellationToken);

        return Summarize(items, shopOptions.Value.ShippingFee);
    }

    /// <summary>Subtotal of available lines; the fee applies only when something can be bought.</summary>
    private static CartResponse Summarize(List<CartItemResponse> items, decimal configuredFee)
    {
        var subtotal = items.Where(item => item.Status == CartItemStatus.Available).Sum(item => item.LineTotal);
        var shippingFee = subtotal > 0 ? configuredFee : 0m;
        var canCheckout = items.Count > 0 && items.TrueForAll(item => item.Status == CartItemStatus.Available);

        return new CartResponse(items, items.Count, subtotal, shippingFee, subtotal + shippingFee, canCheckout);
    }
}
