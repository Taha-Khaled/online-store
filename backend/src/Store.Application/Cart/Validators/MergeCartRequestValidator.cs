using FluentValidation;
using Store.Application.Cart.Requests;
using Store.Domain;

namespace Store.Application.Cart.Validators;

/// <summary>Merge rules (research R9): at most 30 entries, each with a variant id and quantity 1–5.</summary>
public sealed class MergeCartRequestValidator : AbstractValidator<MergeCartRequest>
{
    public MergeCartRequestValidator()
    {
        RuleFor(request => request.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(items => items.Count <= StoreLimits.MaxCartLines)
            .WithMessage($"A guest cart can hold at most {StoreLimits.MaxCartLines} items.");

        RuleForEach(request => request.Items).ChildRules(item =>
        {
            item.RuleFor(entry => entry.VariantId).GreaterThan(0);
            item.RuleFor(entry => entry.Quantity).InclusiveBetween(1, StoreLimits.MaxQuantityPerVariant);
        });
    }
}
