using Shouldly;
using Store.Application.Common;

namespace Store.Infrastructure.Tests.Shop;

/// <summary>Research R3: the shipping fee must be 0 or more.</summary>
public class ShopOptionsValidatorTests
{
    private readonly ShopOptionsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void ZeroOrPositiveFee_Passes(int fee)
    {
        _validator.Validate(null, new ShopOptions { ShippingFee = fee }).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void NegativeFee_FailsNamingTheKey()
    {
        var result = _validator.Validate(null, new ShopOptions { ShippingFee = -1 });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldBe(["'Shop:ShippingFee' must be 0 or more."]);
    }

    [Fact]
    public void Default_Is50()
    {
        new ShopOptions().ShippingFee.ShouldBe(50m);
    }
}
