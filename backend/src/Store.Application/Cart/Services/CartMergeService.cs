using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Cart.Requests;
using Store.Application.Cart.Responses;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
using Store.Domain;
using Store.Domain.Entities;

namespace Store.Application.Cart.Services;

/// <summary>
/// Merges the guest cart into the customer's server cart after login (FR-012, research R9). Each
/// line ends at the higher of the two quantities (never the sum), capped by stock, so merging the
/// same list twice gives the same cart. Entries that cannot be added are reported, not failed.
/// </summary>
public sealed class CartMergeService(
    IAppDbContext db,
    CartReader reader,
    IValidator<MergeCartRequest> validator,
    TimeProvider timeProvider,
    ILogger<CartMergeService> logger)
{
    public async Task<MergeCartResponse> MergeAsync(int userId, MergeCartRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var entries = Collapse(request.Items);

        List<MergeAdjustmentResponse> adjustments;
        try
        {
            adjustments = await MergeOnceAsync(userId, entries, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request changed the same lines first (e.g. inserted one). The max rule makes a rerun safe.
            db.ChangeTracker.Clear();
            adjustments = await MergeOnceAsync(userId, entries, cancellationToken);
        }

        logger.LogInformation(
            "Guest cart merged for user {UserId}: {EntryCount} entries, {AdjustmentCount} adjusted",
            userId,
            entries.Count,
            adjustments.Count);
        return new MergeCartResponse(await reader.GetAsync(userId, cancellationToken), adjustments);
    }

    /// <summary>One entry per variant, keeping the first position and the highest quantity.</summary>
    private static List<MergeCartItem> Collapse(IReadOnlyList<MergeCartItem> items) =>
        items
            .GroupBy(item => item.VariantId)
            .Select(group => new MergeCartItem(group.Key, group.Max(item => item.Quantity)))
            .ToList();

    private async Task<List<MergeAdjustmentResponse>> MergeOnceAsync(
        int userId,
        List<MergeCartItem> entries,
        CancellationToken cancellationToken)
    {
        var variantIds = entries.Select(entry => entry.VariantId).ToList();
        var stocks = await db.ProductVariants
            .AsNoTracking()
            .Where(variant => variantIds.Contains(variant.Id) && variant.Product.IsActive)
            .ToDictionaryAsync(variant => variant.Id, variant => variant.Stock, cancellationToken);
        var lines = await db.CartItems
            .Where(line => line.UserId == userId)
            .ToDictionaryAsync(line => line.ProductVariantId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var adjustments = new List<MergeAdjustmentResponse>();
        try
        {
            foreach (var entry in entries)
            {
                if (MergeEntry(userId, entry, stocks, lines, now) is { } adjustment)
                {
                    adjustments.Add(adjustment);
                }
            }
        }
        catch (DomainException)
        {
            // Backstop (research R1): quantities are 1–5 by validation and stock checks above.
            throw new BadRequestException(
                ErrorCodes.MaxQuantityExceeded,
                $"You can buy at most {StoreLimits.MaxQuantityPerVariant} of each item.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return adjustments;
    }

    /// <summary>Applies one guest entry to the tracked lines; returns an adjustment when it was skipped or reduced.</summary>
    private MergeAdjustmentResponse? MergeEntry(
        int userId,
        MergeCartItem entry,
        Dictionary<int, int> stocks,
        Dictionary<int, CartItem> lines,
        DateTime now)
    {
        if (!stocks.TryGetValue(entry.VariantId, out var stock))
        {
            return Skipped(entry, MergeAdjustmentReason.VariantNotFound);
        }

        if (stock == 0)
        {
            return Skipped(entry, MergeAdjustmentReason.OutOfStock);
        }

        lines.TryGetValue(entry.VariantId, out var line);
        var target = Math.Max(line?.Quantity ?? 0, entry.Quantity);
        var quantity = Math.Min(target, stock);

        if (line is null)
        {
            if (lines.Count >= StoreLimits.MaxCartLines)
            {
                return Skipped(entry, MergeAdjustmentReason.CartFull);
            }

            var added = new CartItem(userId, entry.VariantId, quantity, now);
            db.CartItems.Add(added);
            lines[entry.VariantId] = added;
        }
        else if (line.Quantity != quantity)
        {
            line.SetQuantity(quantity);
        }

        return quantity < target
            ? new MergeAdjustmentResponse(entry.VariantId, MergeAdjustmentReason.QuantityReduced, target, quantity)
            : null;
    }

    private static MergeAdjustmentResponse Skipped(MergeCartItem entry, MergeAdjustmentReason reason) =>
        new(entry.VariantId, reason, entry.Quantity, 0);
}
