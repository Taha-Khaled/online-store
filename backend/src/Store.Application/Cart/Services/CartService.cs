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
/// Cart writes (FR-006–FR-011, research R8). Never reserves or changes stock. Cart lines have no
/// concurrency token: parallel adds of the same variant may lose an increment (accepted, spec
/// Clarifications 2026-11-26); checkout re-validates everything.
/// </summary>
public sealed class CartService(
    IAppDbContext db,
    CartReader reader,
    IValidator<AddCartItemRequest> addValidator,
    IValidator<UpdateCartItemRequest> updateValidator,
    TimeProvider timeProvider,
    ILogger<CartService> logger)
{
    public async Task<CartResponse> AddItemAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        await addValidator.ValidateOrThrowAsync(request, cancellationToken);

        try
        {
            await AddOnceAsync(userId, request, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException)
        {
            // Another request inserted the same line first; the unique index stopped this one.
            db.ChangeTracker.Clear();
            if (!await LineExistsAsync(userId, request.VariantId, cancellationToken))
            {
                throw;
            }

            await AddOnceAsync(userId, request, cancellationToken);
        }

        return await reader.GetAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> UpdateItemAsync(
        int userId,
        int itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateOrThrowAsync(request, cancellationToken);

        var line = await db.CartItems.SingleOrDefaultAsync(
                candidate => candidate.Id == itemId && candidate.UserId == userId,
                cancellationToken)
            ?? throw CartItemNotFound();

        var stock = await LoadStockAsync(line.ProductVariantId, cancellationToken);
        EnsureQuantityAllowed(line.ProductVariantId, request.Quantity, stock);
        ApplyDomainRule(() => line.SetQuantity(request.Quantity));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The line was removed by another request in the meantime.
            throw CartItemNotFound();
        }

        logger.LogInformation("Cart line {ItemId} of user {UserId} set to {Quantity}", itemId, userId, request.Quantity);
        return await reader.GetAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> RemoveItemAsync(int userId, int itemId, CancellationToken cancellationToken = default)
    {
        var removed = await db.CartItems
            .Where(line => line.Id == itemId && line.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed == 0)
        {
            throw CartItemNotFound();
        }

        return await reader.GetAsync(userId, cancellationToken);
    }

    public async Task ClearAsync(int userId, CancellationToken cancellationToken = default)
    {
        var removed = await db.CartItems.Where(line => line.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        logger.LogInformation("Cart of user {UserId} cleared ({LineCount} lines)", userId, removed);
    }

    /// <summary>Adds to an existing line or creates one, enforcing the 5-unit, stock and 30-line limits.</summary>
    private async Task AddOnceAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var stock = await LoadStockAsync(request.VariantId, cancellationToken);
        var line = await db.CartItems.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId && candidate.ProductVariantId == request.VariantId,
            cancellationToken);

        var quantity = (line?.Quantity ?? 0) + request.Quantity;
        EnsureQuantityAllowed(request.VariantId, quantity, stock);

        if (line is null)
        {
            await EnsureCartHasRoomAsync(userId, cancellationToken);
            var addedAt = timeProvider.GetUtcNow().UtcDateTime;
            ApplyDomainRule(() => db.CartItems.Add(new CartItem(userId, request.VariantId, quantity, addedAt)));
        }
        else
        {
            ApplyDomainRule(() => line.SetQuantity(quantity));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cart of user {UserId}: variant {VariantId} now {Quantity}", userId, request.VariantId, quantity);
    }

    /// <summary>Current stock of a variant whose product is active; otherwise 404 <c>variantNotFound</c>.</summary>
    private async Task<int> LoadStockAsync(int variantId, CancellationToken cancellationToken) =>
        await db.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.Id == variantId && variant.Product.IsActive)
            .Select(variant => (int?)variant.Stock)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(ErrorCodes.VariantNotFound, "This product is not available.");

    private async Task EnsureCartHasRoomAsync(int userId, CancellationToken cancellationToken)
    {
        var lines = await db.CartItems.CountAsync(line => line.UserId == userId, cancellationToken);
        if (lines >= StoreLimits.MaxCartLines)
        {
            throw new ConflictException(
                ErrorCodes.CartFull,
                $"Your cart can hold at most {StoreLimits.MaxCartLines} different items.");
        }
    }

    private Task<bool> LineExistsAsync(int userId, int variantId, CancellationToken cancellationToken) =>
        db.CartItems.AnyAsync(line => line.UserId == userId && line.ProductVariantId == variantId, cancellationToken);

    private static void EnsureQuantityAllowed(int variantId, int quantity, int stock)
    {
        if (quantity > StoreLimits.MaxQuantityPerVariant)
        {
            throw MaxQuantityExceeded();
        }

        if (quantity > stock)
        {
            var maxQuantity = Math.Min(stock, StoreLimits.MaxQuantityPerVariant);
            throw new ConflictException(
                ErrorCodes.InsufficientStock,
                "Not enough stock for the requested quantity.",
                new { variantId, maxQuantity });
        }
    }

    /// <summary>Backstop: the quantity rule is checked first, so a domain rejection maps to the same error (research R1).</summary>
    private static void ApplyDomainRule(Action change)
    {
        try
        {
            change();
        }
        catch (DomainException)
        {
            throw MaxQuantityExceeded();
        }
    }

    private static BadRequestException MaxQuantityExceeded() =>
        new(ErrorCodes.MaxQuantityExceeded, $"You can buy at most {StoreLimits.MaxQuantityPerVariant} of each item.");

    private static NotFoundException CartItemNotFound() =>
        new(ErrorCodes.CartItemNotFound, "This cart item was not found.");
}
