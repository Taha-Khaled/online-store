using FluentValidation;
using Store.Application.Common.Validation;
using Store.Application.Orders.Requests;

namespace Store.Application.Orders.Validators;

/// <summary>Checkout rules (FR-014). Runs on the trimmed request.</summary>
public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 100;
    public const int MinCityLength = 2;
    public const int MaxCityLength = 100;
    public const int MinAddressLength = 5;
    public const int MaxAddressLength = 300;
    public const int MaxNotesLength = 500;

    public PlaceOrderRequestValidator()
    {
        RuleFor(request => request.ShippingName)
            .NotEmpty()
            .Length(MinNameLength, MaxNameLength);

        RuleFor(request => request.ShippingPhone)
            .NotEmpty()
            .MaximumLength(PhoneNumbers.MaxLength)
            .Must(PhoneNumbers.IsValid)
            .WithMessage(PhoneNumbers.InvalidMessage);

        RuleFor(request => request.City)
            .NotEmpty()
            .Length(MinCityLength, MaxCityLength);

        RuleFor(request => request.AddressLine)
            .NotEmpty()
            .Length(MinAddressLength, MaxAddressLength);

        RuleFor(request => request.Notes)
            .MaximumLength(MaxNotesLength);
    }
}
