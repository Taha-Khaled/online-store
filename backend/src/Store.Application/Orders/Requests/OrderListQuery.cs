namespace Store.Application.Orders.Requests;

/// <summary>Paging for the customer's order list (contracts/api.md "GET /api/orders").</summary>
public sealed record OrderListQuery
{
    public const int DefaultPageSize = 10;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
