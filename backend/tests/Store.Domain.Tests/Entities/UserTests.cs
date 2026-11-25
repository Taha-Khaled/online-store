using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Domain.Tests.Entities;

/// <summary>FR-027: a user is only ever created with a real password hash (data-model.md User.PasswordHash).</summary>
public class UserTests
{
    [Fact]
    public void Constructor_WithHash_SetsAllFields()
    {
        var createdAt = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

        var user = new User("Demo Customer", "customer@store.local", "01000000002", "hash-value", Role.Customer, createdAt);

        user.FullName.ShouldBe("Demo Customer");
        user.Email.ShouldBe("customer@store.local");
        user.Phone.ShouldBe("01000000002");
        user.PasswordHash.ShouldBe("hash-value");
        user.Role.ShouldBe(Role.Customer);
        user.CreatedAt.ShouldBe(createdAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithoutHash_Throws(string? passwordHash)
    {
        Should.Throw<ArgumentException>(() =>
            new User("Demo Customer", "customer@store.local", "01000000002", passwordHash!, Role.Customer, DateTime.UtcNow));
    }
}
