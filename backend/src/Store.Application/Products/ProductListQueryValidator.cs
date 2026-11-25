using FluentValidation;

namespace Store.Application.Products;

/// <summary>Product list rules (FR-032, FR-033, FR-036, FR-037; research R7).</summary>
public sealed class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    public const int MaxPageSize = 48;
    public const int MaxSearchLength = 100;

    public ProductListQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Sort).IsInEnum();

        RuleFor(query => query.Search)
            .Must(search => search is null || search.Trim().Length <= MaxSearchLength)
            .WithMessage($"Search must be {MaxSearchLength} characters or fewer.");

        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0).When(query => query.MinPrice.HasValue);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0).When(query => query.MaxPrice.HasValue);
        RuleFor(query => query.MinPrice)
            .LessThanOrEqualTo(query => query.MaxPrice!.Value)
            .When(query => query.MinPrice.HasValue && query.MaxPrice.HasValue)
            .WithMessage("Min price must not be greater than max price.");
    }
}
