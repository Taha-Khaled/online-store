using FluentValidation;
using Store.Application.Auth.Requests;
using Store.Application.Common.Validation;

namespace Store.Application.Auth.Validators;

/// <summary>Registration rules (FR-013). Runs on the normalized (trimmed, lowercased email) request.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinFullNameLength = 2;
    public const int MaxFullNameLength = 100;
    public const int MaxEmailLength = 256;
    public const int MaxPhoneLength = PhoneNumbers.MaxLength;
    public const int MinPasswordLength = 8;

    /// <summary>Upper bound for any password the API hashes or verifies (register and login).</summary>
    public const int MaxPasswordLength = 128;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .Length(MinFullNameLength, MaxFullNameLength);

        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(MaxEmailLength)
            .EmailAddress();

        RuleFor(request => request.Phone)
            .NotEmpty()
            .MaximumLength(MaxPhoneLength)
            .Must(PhoneNumbers.IsValid)
            .WithMessage(PhoneNumbers.InvalidMessage);

        RuleFor(request => request.Password)
            .NotEmpty()
            .Length(MinPasswordLength, MaxPasswordLength)
            .Must(password => password.Any(char.IsLetter)).WithMessage("Password must contain at least one letter.")
            .Must(password => password.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.");
    }
}
