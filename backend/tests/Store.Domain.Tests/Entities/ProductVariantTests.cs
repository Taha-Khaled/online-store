using Shouldly;
using Store.Domain;
using Store.Domain.Entities;

namespace Store.Domain.Tests.Entities;

/// <summary>Stock rules (data-model.md "Changed: ProductVariant"; constitution IV).</summary>
public class ProductVariantTests
{
    [Fact]
    public void Deduct_ToExactlyZero_IsAllowed()
    {
        var variant = CreateVariant(stock: 3);

        variant.Deduct(3);

        variant.Stock.ShouldBe(0);
    }

    [Fact]
    public void Deduct_LeavesTheRemainder()
    {
        var variant = CreateVariant(stock: 20);

        variant.Deduct(2);

        variant.Stock.ShouldBe(18);
    }

    [Fact]
    public void Deduct_MoreThanStock_ThrowsAndKeepsStock()
    {
        var variant = CreateVariant(stock: 1);

        Should.Throw<DomainException>(() => variant.Deduct(2));

        variant.Stock.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deduct_NonPositiveQuantity_Throws(int quantity)
    {
        var variant = CreateVariant(stock: 5);

        Should.Throw<DomainException>(() => variant.Deduct(quantity));

        variant.Stock.ShouldBe(5);
    }

    [Fact]
    public void Restock_AddsUnits()
    {
        var variant = CreateVariant(stock: 0);

        variant.Restock(2);

        variant.Stock.ShouldBe(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Restock_NonPositiveQuantity_Throws(int quantity)
    {
        var variant = CreateVariant(stock: 4);

        Should.Throw<DomainException>(() => variant.Restock(quantity));

        variant.Stock.ShouldBe(4);
    }

    private static ProductVariant CreateVariant(int stock) => new(1, 1, 1, "MTS-001-BLK-M", stock);
}
