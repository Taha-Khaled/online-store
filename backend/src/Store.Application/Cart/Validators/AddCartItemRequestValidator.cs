using FluentValidation;
using Store.Application.Cart.Requests;
using Store.Domain;

namespace Store.Application.Cart.Validators;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.VariantId).GreaterThan(0);
        RuleFor(request => request.Quantity).InclusiveBetween(1, StoreLimits.MaxQuantityPerVariant);
    }
}
