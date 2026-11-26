using FluentValidation;
using Store.Application.Auth.Requests;

namespace Store.Application.Auth.Validators;

/// <summary>
/// Login checks presence and the password length cap only; the registration password rules do
/// not apply (FR-014).
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty();
        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(RegisterRequestValidator.MaxPasswordLength);
    }
}
