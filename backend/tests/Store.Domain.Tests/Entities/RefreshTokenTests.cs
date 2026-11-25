using Shouldly;
using Store.Domain.Entities;

namespace Store.Domain.Tests.Entities;

/// <summary>RefreshToken states and transitions (data-model.md "Domain behavior").</summary>
public class RefreshTokenTests
{
    private static readonly DateTime Created = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Expires = Created.AddDays(7);

    [Fact]
    public void Constructor_SetsFields_AndStartsActive()
    {
        var token = CreateToken();

        token.UserId.ShouldBe(2);
        token.TokenHash.ShouldBe("HASH-A");
        token.CreatedAt.ShouldBe(Created);
        token.ExpiresAt.ShouldBe(Expires);
        token.RevokedAt.ShouldBeNull();
        token.ReplacedByTokenHash.ShouldBeNull();
        token.IsRotated.ShouldBeFalse();
        token.IsActive(Created).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithoutHash_Throws(string? tokenHash)
    {
        Should.Throw<ArgumentException>(() => new RefreshToken(2, tokenHash!, Created, Expires));
    }

    [Fact]
    public void Constructor_ExpiryNotAfterCreation_Throws()
    {
        Should.Throw<ArgumentException>(() => new RefreshToken(2, "HASH-A", Created, Created));
    }

    [Fact]
    public void IsActive_UntilJustBeforeExpiry_InactiveFromExpiry()
    {
        var token = CreateToken();

        token.IsActive(Expires.AddTicks(-1)).ShouldBeTrue();
        token.IsActive(Expires).ShouldBeFalse();
        token.IsActive(Expires.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_MakesInactive_WithoutRotation()
    {
        var token = CreateToken();

        token.Revoke(Created.AddHours(1));

        token.RevokedAt.ShouldBe(Created.AddHours(1));
        token.IsActive(Created.AddHours(2)).ShouldBeFalse();
        token.IsRotated.ShouldBeFalse();
    }

    [Fact]
    public void Revoke_Twice_KeepsTheFirstTime()
    {
        var token = CreateToken();

        token.Revoke(Created.AddHours(1));
        token.Revoke(Created.AddHours(5));

        token.RevokedAt.ShouldBe(Created.AddHours(1));
    }

    [Fact]
    public void MarkRotated_SetsRevokedAtAndReplacement()
    {
        var token = CreateToken();

        token.MarkRotated(Created.AddMinutes(30), "HASH-B");

        token.RevokedAt.ShouldBe(Created.AddMinutes(30));
        token.ReplacedByTokenHash.ShouldBe("HASH-B");
        token.IsRotated.ShouldBeTrue();
        token.IsActive(Created.AddMinutes(31)).ShouldBeFalse();
    }

    [Fact]
    public void MarkRotated_RevokedToken_Throws()
    {
        var token = CreateToken();
        token.Revoke(Created.AddMinutes(1));

        Should.Throw<InvalidOperationException>(() => token.MarkRotated(Created.AddMinutes(2), "HASH-B"));
        token.IsRotated.ShouldBeFalse();
    }

    [Fact]
    public void MarkRotated_ExpiredToken_Throws()
    {
        var token = CreateToken();

        Should.Throw<InvalidOperationException>(() => token.MarkRotated(Expires, "HASH-B"));
        token.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void MarkRotated_AlreadyRotated_Throws()
    {
        var token = CreateToken();
        token.MarkRotated(Created.AddMinutes(1), "HASH-B");

        Should.Throw<InvalidOperationException>(() => token.MarkRotated(Created.AddMinutes(2), "HASH-C"));
        token.ReplacedByTokenHash.ShouldBe("HASH-B");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MarkRotated_WithoutReplacementHash_Throws(string replacedByHash)
    {
        var token = CreateToken();

        Should.Throw<ArgumentException>(() => token.MarkRotated(Created.AddMinutes(1), replacedByHash));
        token.RevokedAt.ShouldBeNull();
    }

    private static RefreshToken CreateToken() => new(2, "HASH-A", Created, Expires);
}
