using FluentValidation;

namespace Store.Application.Auth;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty();
    }
}
