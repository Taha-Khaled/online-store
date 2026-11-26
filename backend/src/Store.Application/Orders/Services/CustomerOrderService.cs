using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Common;
using Store.Application.Common.Errors;
using Store.Application.Common.Validation;
using Store.Application.Orders.Requests;
using Store.Application.Orders.Responses;
using Store.Domain;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Application.Orders.Services;

/// <summary>
/// The customer's own orders: list, details and cancel (FR-021–FR-024, research R6, R12). Another
/// customer's order is reported as not found. A customer may cancel only a Pending order; the
/// cancel returns the stock and is guarded by the order's and the variants' rowversions.
/// </summary>
public sealed class CustomerOrderService(
    IAppDbContext db,
    IValidator<OrderListQuery> listValidator,
    TimeProvider timeProvider,
    ILogger<CustomerOrderService> logger)
{
    public async Task<PagedResponse<OrderSummaryResponse>> ListAsync(
        int userId,
        OrderListQuery query,
        CancellationToken cancellationToken = default)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);

        var orders = db.Orders.AsNoTracking().Where(order => order.UserId == userId);
        var totalCount = await orders.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResponse<OrderSummaryResponse>([], query.Page, query.PageSize, totalCount, totalPages);
        }

        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(order => new OrderSummaryResponse(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.Items.Count,
                order.Total,
                order.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderSummaryResponse>(items, query.Page, query.PageSize, totalCount, totalPages);
    }

    public async Task<OrderDetailsResponse> GetAsync(int userId, int orderId, CancellationToken cancellationToken = default) =>
        await db.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Select(OrderMapping.ToDetailsExpression)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw OrderNotFound();

    public async Task<OrderDetailsResponse> CancelAsync(int userId, int orderId, CancellationToken cancellationToken = default)
    {
        var order = await ConcurrencyRetry.RunAsync(
            db,
            token => CancelOnceAsync(userId, orderId, token),
            () => OrderConflict(userId, orderId),
            cancellationToken);

        return OrderMapping.ToDetails(order);
    }

    /// <summary>One attempt; runs inside the transaction opened by <see cref="ConcurrencyRetry"/>.</summary>
    private async Task<Order> CancelOnceAsync(int userId, int orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(candidate => candidate.Items).ThenInclude(item => item.ProductVariant)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId && candidate.UserId == userId, cancellationToken)
            ?? throw OrderNotFound();

        if (order.Status != OrderStatus.Pending)
        {
            throw CannotBeCancelled(userId, orderId);
        }

        try
        {
            order.Cancel(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainException)
        {
            // Backstop (research R1): the Pending check above normally reports this first.
            throw CannotBeCancelled(userId, orderId);
        }

        foreach (var item in order.Items)
        {
            item.ProductVariant.Restock(item.Quantity);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Order {OrderId} of user {UserId} cancelled; stock returned for {LineCount} lines", orderId, userId, order.Items.Count);
        return order;
    }

    private ConflictException CannotBeCancelled(int userId, int orderId)
    {
        logger.LogInformation("Cancel of order {OrderId} by user {UserId} refused: not Pending", orderId, userId);
        return new ConflictException(ErrorCodes.OrderCannotBeCancelled, "Only a pending order can be cancelled.");
    }

    private ConflictException OrderConflict(int userId, int orderId)
    {
        logger.LogWarning(
            "Cancel of order {OrderId} by user {UserId} gave up after {Attempts} conflicting attempts",
            orderId,
            userId,
            ConcurrencyRetry.MaxAttempts);
        return new ConflictException(ErrorCodes.OrderConflict, "The order could not be cancelled right now. Please try again.");
    }

    private static NotFoundException OrderNotFound() => new(ErrorCodes.OrderNotFound, "This order was not found.");
}
