using FluentValidation;

namespace Store.Application.Auth;

/// <summary>Registration rules (FR-013). Runs on the normalized (trimmed, lowercased email) request.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinFullNameLength = 2;
    public const int MaxFullNameLength = 100;
    public const int MaxEmailLength = 256;
    public const int MaxPhoneLength = 20;
    public const int MinPasswordLength = 8;

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
            .Must(IsPhoneNumber)
            .WithMessage("Phone must contain digits only, with an optional leading +.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .Must(password => password.Any(char.IsLetter)).WithMessage("Password must contain at least one letter.")
            .Must(password => password.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.");
    }

    private static bool IsPhoneNumber(string phone)
    {
        var digits = phone.StartsWith('+') ? phone[1..] : phone;
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
    }
}
