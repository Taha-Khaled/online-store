using Microsoft.Extensions.Options;

namespace Store.Application.Common;

/// <summary>The "Shop" configuration section (research R3). Not a secret.</summary>
public sealed class ShopOptions
{
    public const string SectionName = "Shop";
    public const decimal DefaultShippingFee = 50m;

    /// <summary>Fixed shipping fee in EGP stored on every new order (constitution Business Rules).</summary>
    public decimal ShippingFee { get; set; } = DefaultShippingFee;
}

/// <summary>Stops startup with a message naming the key when the fee is negative.</summary>
public sealed class ShopOptionsValidator : IValidateOptions<ShopOptions>
{
    public ValidateOptionsResult Validate(string? name, ShopOptions options) =>
        options.ShippingFee < 0
            ? ValidateOptionsResult.Fail("'Shop:ShippingFee' must be 0 or more.")
            : ValidateOptionsResult.Success;
}
