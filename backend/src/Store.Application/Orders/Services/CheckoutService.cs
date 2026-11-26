using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.Cart.Responses;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
using Store.Application.Orders.Requests;
using Store.Application.Orders.Responses;
using Store.Domain;
using Store.Domain.Entities;

namespace Store.Application.Orders.Services;

/// <summary>
/// Cash-on-delivery checkout (FR-014–FR-020, research R5). Each attempt re-reads the cart and stock,
/// deducts stock through the domain, creates the Pending order and empties the cart in one
/// transaction. Every variant UPDATE carries the variant's rowversion, so a competing checkout makes
/// this attempt fail as a whole and the next attempt sees the real stock: the store never oversells.
/// </summary>
public sealed class CheckoutService(
    IAppDbContext db,
    IOrderNumberGenerator orderNumbers,
    IValidator<PlaceOrderRequest> validator,
    IOptions<ShopOptions> shopOptions,
    TimeProvider timeProvider,
    ILogger<CheckoutService> logger)
{
    public async Task<OrderDetailsResponse> CheckoutAsync(
        int userId,
        PlaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var shipping = Normalize(request);
        await validator.ValidateOrThrowAsync(shipping, cancellationToken);

        var attempt = 0;
        var order = await ConcurrencyRetry.RunAsync(
            db,
            token => PlaceOnceAsync(userId, shipping, ++attempt, token),
            () => CheckoutConflict(userId),
            cancellationToken);

        return OrderMapping.ToDetails(order);
    }

    /// <summary>One attempt; runs inside the transaction opened by <see cref="ConcurrencyRetry"/>.</summary>
    private async Task<Order> PlaceOnceAsync(
        int userId,
        PlaceOrderRequest shipping,
        int attempt,
        CancellationToken cancellationToken)
    {
        var lines = await LoadCartAsync(userId, cancellationToken);
        if (lines.Count == 0)
        {
            throw new BadRequestException(ErrorCodes.CartEmpty, "Your cart is empty.");
        }

        EnsureAllAvailable(userId, lines);
        var items = lines.Select(Purchase).ToList();

        var orderNumber = await orderNumbers.NextAsync(cancellationToken);
        var order = Order.CreatePending(
            orderNumber,
            userId,
            new ShippingDetails(shipping.ShippingName, shipping.ShippingPhone, shipping.City, shipping.AddressLine, shipping.Notes),
            items,
            shopOptions.Value.ShippingFee,
            timeProvider.GetUtcNow().UtcDateTime);

        db.Orders.Add(order);
        db.CartItems.RemoveRange(lines);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Order {OrderNumber} placed by user {UserId} ({LineCount} lines, attempt {Attempt})",
            order.OrderNumber,
            userId,
            items.Count,
            attempt);
        return order;
    }

    /// <summary>The customer's cart lines, tracked, with what the stock check and the snapshots need.</summary>
    private Task<List<CartItem>> LoadCartAsync(int userId, CancellationToken cancellationToken) =>
        db.CartItems
            .Include(line => line.ProductVariant).ThenInclude(variant => variant.Product)
            .Include(line => line.ProductVariant).ThenInclude(variant => variant.Color)
            .Include(line => line.ProductVariant).ThenInclude(variant => variant.Size)
            .Where(line => line.UserId == userId)
            .OrderBy(line => line.AddedAt)
            .ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

    /// <summary>FR-016: nothing is created when any line is no longer purchasable as it stands.</summary>
    private void EnsureAllAvailable(int userId, List<CartItem> lines)
    {
        var changed = lines
            .Select(line => (Line: line, Status: StatusOf(line)))
            .Where(entry => entry.Status != CartItemStatus.Available)
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        logger.LogInformation("Checkout of user {UserId} refused: {ChangedCount} cart lines changed", userId, changed.Count);
        throw CartChanged(changed);
    }

    /// <summary>Deducts the line's stock and snapshots the current catalog data for the order.</summary>
    private static OrderItem Purchase(CartItem line)
    {
        var variant = line.ProductVariant;
        try
        {
            variant.Deduct(line.Quantity);
        }
        catch (DomainException)
        {
            // Backstop (research R1): the stock check above normally reports this first.
            throw CartChanged([(line, CartItemStatus.QuantityReduced)]);
        }

        return new OrderItem(
            variant.Id,
            variant.Product.Name,
            variant.Color.Name,
            variant.Size.Name,
            variant.Product.Price,
            line.Quantity);
    }

    /// <summary>Same rules as the cart's SQL status (research R7), on loaded data.</summary>
    private static CartItemStatus StatusOf(CartItem line)
    {
        var variant = line.ProductVariant;
        if (!variant.Product.IsActive)
        {
            return CartItemStatus.Unavailable;
        }

        if (variant.Stock == 0)
        {
            return CartItemStatus.OutOfStock;
        }

        return variant.Stock < line.Quantity ? CartItemStatus.QuantityReduced : CartItemStatus.Available;
    }

    private static ConflictException CartChanged(IEnumerable<(CartItem Line, CartItemStatus Status)> changed) =>
        new(
            ErrorCodes.CartChanged,
            "Some items in your cart changed. Review your cart and try again.",
            new
            {
                items = changed.Select(entry => new
                {
                    itemId = entry.Line.Id,
                    variantId = entry.Line.ProductVariantId,
                    status = entry.Status,
                    maxQuantity = Math.Min(entry.Line.ProductVariant.Stock, StoreLimits.MaxQuantityPerVariant)
                }).ToList()
            });

    private ConflictException CheckoutConflict(int userId)
    {
        logger.LogWarning(
            "Checkout of user {UserId} gave up after {Attempts} conflicting attempts",
            userId,
            ConcurrencyRetry.MaxAttempts);
        return new ConflictException(ErrorCodes.CheckoutConflict, "Your order could not be placed right now. Please try again.");
    }

    private static PlaceOrderRequest Normalize(PlaceOrderRequest request) =>
        new(
            request.ShippingName?.Trim() ?? string.Empty,
            request.ShippingPhone?.Trim() ?? string.Empty,
            request.City?.Trim() ?? string.Empty,
            request.AddressLine?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());
}
