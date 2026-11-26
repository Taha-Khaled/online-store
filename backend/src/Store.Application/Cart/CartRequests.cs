using FluentValidation;
using Store.Domain;

namespace Store.Application.Cart;

public sealed record AddCartItemRequest(int VariantId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.VariantId).GreaterThan(0);
        RuleFor(request => request.Quantity).InclusiveBetween(1, StoreLimits.MaxQuantityPerVariant);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(request => request.Quantity).InclusiveBetween(1, StoreLimits.MaxQuantityPerVariant);
    }
}
