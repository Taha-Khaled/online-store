using FluentValidation;
using Store.Application.Orders.Requests;

namespace Store.Application.Orders.Validators;

/// <summary>Order list paging rules (research R12).</summary>
public sealed class OrderListQueryValidator : AbstractValidator<OrderListQuery>
{
    public const int MaxPageSize = 50;

    public OrderListQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
