using System.Text;
using Microsoft.Extensions.Options;

namespace Store.Infrastructure.Security;

/// <summary>Reports every missing or invalid JWT setting by its key (FR-009, research R3).</summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            failures.Add("'Jwt:Key' is not configured.");
        }
        else if (Encoding.UTF8.GetByteCount(options.Key) < JwtOptions.MinimumKeyBytes)
        {
            failures.Add($"'Jwt:Key' must be at least {JwtOptions.MinimumKeyBytes} bytes (UTF-8).");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("'Jwt:Issuer' is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("'Jwt:Audience' is not configured.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
