using FluentValidation;
using Store.Application.Auth.Requests;

namespace Store.Application.Auth.Validators;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty();
    }
}
