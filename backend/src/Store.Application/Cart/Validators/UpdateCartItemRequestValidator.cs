using FluentValidation;
using Store.Application.Cart.Requests;
using Store.Domain;

namespace Store.Application.Cart.Validators;

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(request => request.Quantity).InclusiveBetween(1, StoreLimits.MaxQuantityPerVariant);
    }
}
