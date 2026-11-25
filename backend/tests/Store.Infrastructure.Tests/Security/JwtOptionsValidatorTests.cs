using Shouldly;
using Store.Infrastructure.Security;

namespace Store.Infrastructure.Tests.Security;

/// <summary>FR-009 / research R3: every missing or invalid JWT setting is reported by its key.</summary>
public class JwtOptionsValidatorTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef"; // exactly 32 bytes

    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void ValidOptions_Pass()
    {
        var result = _validator.Validate(null, Options(ValidKey, "store-api", "store-web"));

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "store-api", "store-web", "'Jwt:Key' is not configured.")]
    [InlineData("  ", "store-api", "store-web", "'Jwt:Key' is not configured.")]
    [InlineData("0123456789abcdef0123456789abcde", "store-api", "store-web", "'Jwt:Key' must be at least 32 bytes (UTF-8).")]
    [InlineData(ValidKey, null, "store-web", "'Jwt:Issuer' is not configured.")]
    [InlineData(ValidKey, "store-api", "", "'Jwt:Audience' is not configured.")]
    public void EachProblem_IsReportedByItsKey(string? key, string? issuer, string? audience, string expected)
    {
        var result = _validator.Validate(null, Options(key, issuer, audience));

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldBe([expected]);
    }

    [Fact]
    public void KeyLength_CountsUtf8Bytes()
    {
        // 16 two-byte characters = 32 bytes.
        var result = _validator.Validate(null, Options(new string('é', 16), "store-api", "store-web"));

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SeveralProblems_AreReportedTogether()
    {
        var result = _validator.Validate(null, Options(null, null, null));

        result.Failures.ShouldBe(
        [
            "'Jwt:Key' is not configured.",
            "'Jwt:Issuer' is not configured.",
            "'Jwt:Audience' is not configured."
        ]);
    }

    [Fact]
    public void CreateSigningKey_WithoutKey_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Options(null, "a", "b").CreateSigningKey());
    }

    private static JwtOptions Options(string? key, string? issuer, string? audience) =>
        new() { Key = key, Issuer = issuer, Audience = audience };
}
