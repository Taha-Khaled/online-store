using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Security;
using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Store.Domain.Entities.User>;

namespace Store.Infrastructure.Tests.Security;

/// <summary>FR-026 / AC 12: the application hasher and compatibility with 001 hashes.</summary>
public class PasswordHasherTests
{
    private const string Password = "Correct#Horse-42";

    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_IsNotThePasswordAndVerifies()
    {
        var hash = _hasher.Hash(Password);

        hash.ShouldNotBe(Password);
        hash.ShouldNotContain(Password);
        _hasher.Verify(hash, Password).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify(hash, "correct#horse-42").ShouldBeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentSaltedHashes()
    {
        _hasher.Hash(Password).ShouldNotBe(_hasher.Hash(Password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_EmptyPassword_Throws(string? password)
    {
        Should.Throw<ArgumentException>(() => _hasher.Hash(password!));
    }

    [Fact]
    public void Verify_HashCreatedLikeThe001Seeder_Succeeds()
    {
        // 001 hashed with PasswordHasher<User> and a real user instance.
        var user = new User("Store Admin", "admin@store.local", "01000000001", "placeholder", Role.Admin, DateTime.UtcNow);
        var legacyHash = new IdentityHasher().HashPassword(user, Password);

        _hasher.Verify(legacyHash, Password).ShouldBeTrue();
        _hasher.Verify(legacyHash, "wrong").ShouldBeFalse();
    }
}
