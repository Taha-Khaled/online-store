using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void SetPasswordHash_ReplacesHash()
    {
        var user = CreateUser();

        user.SetPasswordHash("new-hash");

        user.PasswordHash.ShouldBe("new-hash");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SetPasswordHash_EmptyValue_Throws(string passwordHash)
    {
        var user = CreateUser();

        Should.Throw<ArgumentException>(() => user.SetPasswordHash(passwordHash));
        user.PasswordHash.ShouldBe("old-hash");
    }

    private static User CreateUser() =>
        new("Demo Customer", "customer@store.local", "01000000002", "old-hash", Role.Customer, DateTime.UtcNow);
}
