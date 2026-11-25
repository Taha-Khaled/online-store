using Shouldly;
using Store.Domain;
using Store.Domain.Entities;

namespace Store.Domain.Tests.Entities;

/// <summary>Cart line quantity rule (data-model.md "Changed: CartItem").</summary>
public class CartItemTests
{
    private static readonly DateTime AddedAt = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_AcceptsOneToFive(int quantity)
    {
        var item = new CartItem(2, 10, quantity, AddedAt);

        item.Quantity.ShouldBe(quantity);
        item.UserId.ShouldBe(2);
        item.ProductVariantId.ShouldBe(10);
        item.AddedAt.ShouldBe(AddedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Constructor_RejectsOutOfRange(int quantity)
    {
        Should.Throw<DomainException>(() => new CartItem(2, 10, quantity, AddedAt));
    }

    [Fact]
    public void SetQuantity_ChangesTheValue()
    {
        var item = new CartItem(2, 10, 1, AddedAt);

        item.SetQuantity(4);

        item.Quantity.ShouldBe(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void SetQuantity_RejectsOutOfRange_AndKeepsTheValue(int quantity)
    {
        var item = new CartItem(2, 10, 3, AddedAt);

        Should.Throw<DomainException>(() => item.SetQuantity(quantity));

        item.Quantity.ShouldBe(3);
    }
}
