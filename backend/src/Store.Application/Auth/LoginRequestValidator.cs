using FluentValidation;

namespace Store.Application.Auth;

/// <summary>Login only checks presence; the registration password rules do not apply (FR-014).</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty();
        RuleFor(request => request.Password).NotEmpty();
    }
}
