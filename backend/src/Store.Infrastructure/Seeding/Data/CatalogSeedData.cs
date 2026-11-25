namespace Store.Infrastructure.Seeding.Data;

public sealed record CategorySeed(string Name, string Slug, string? ParentSlug, string? SkuCode);

public sealed record ProductSeed(
    int Number,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    bool IsFeatured,
    string CategorySlug,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Sizes);

public sealed record SpecialStockSeed(string ProductSlug, string Color, string Size, int Stock);

/// <summary>Catalog seed content (spec.md "Seed Data"). Color order matters: it feeds the stock rule.</summary>
public static class CatalogSeedData
{
    private static readonly string[] ClothingSizes = ["XS", "S", "M", "L", "XL", "XXL"];
    private static readonly string[] ShoeSizes = ["38", "39", "40", "41", "42", "43", "44", "45"];

    public static readonly IReadOnlyList<CategorySeed> Categories =
    [
        new("Men", "men", null, null),
        new("Women", "women", null, null),
        new("Shoes", "shoes", null, null),
        new("T-Shirts", "men-t-shirts", "men", "MTS"),
        new("Shirts", "men-shirts", "men", "MSH"),
        new("Pants", "men-pants", "men", "MPT"),
        new("Dresses", "women-dresses", "women", "WDR"),
        new("Tops", "women-tops", "women", "WTP"),
        new("Pants", "women-pants", "women", "WPT"),
        new("Sneakers", "shoes-sneakers", "shoes", "SSN"),
        new("Formal", "shoes-formal", "shoes", "SFM"),
        new("Sandals", "shoes-sandals", "shoes", "SSD")
    ];

    public static readonly IReadOnlyDictionary<string, string> ColorCodes = new Dictionary<string, string>
    {
        ["Black"] = "BLK",
        ["White"] = "WHT",
        ["Navy"] = "NVY",
        ["Gray"] = "GRY",
        ["Beige"] = "BEG",
        ["Red"] = "RED",
        ["Blue"] = "BLU",
        ["Brown"] = "BRN"
    };

    public static readonly IReadOnlyList<ProductSeed> Products =
    [
        new(1, "Classic Cotton T-Shirt", "classic-cotton-t-shirt",
            "A soft everyday crew-neck T-shirt in breathable cotton.",
            350m, true, "men-t-shirts", ["Black", "White", "Navy"], Clothing("S", "XL")),
        new(2, "Graphic Oversized Tee", "graphic-oversized-tee",
            "A relaxed oversized tee with a bold front print.",
            450m, false, "men-t-shirts", ["Black", "Gray"], Clothing("S", "XXL")),
        new(3, "Oxford Button-Down Shirt", "oxford-button-down-shirt",
            "A classic Oxford cotton shirt with a button-down collar.",
            750m, true, "men-shirts", ["White", "Blue"], Clothing("S", "XL")),
        new(4, "Linen Summer Shirt", "linen-summer-shirt",
            "A lightweight linen shirt for hot summer days.",
            850m, false, "men-shirts", ["Beige", "White"], Clothing("M", "XL")),
        new(5, "Slim Fit Chinos", "slim-fit-chinos",
            "Tailored slim-fit chinos in stretch cotton twill.",
            900m, false, "men-pants", ["Beige", "Navy", "Black"], Clothing("S", "XL")),
        new(6, "Classic Denim Jeans", "classic-denim-jeans",
            "Straight-leg jeans in durable mid-weight denim.",
            1100m, true, "men-pants", ["Blue", "Black"], Clothing("S", "XXL")),
        new(7, "Floral Midi Dress", "floral-midi-dress",
            "A flowing midi dress with an all-over floral print.",
            1250m, true, "women-dresses", ["Red", "Navy"], Clothing("XS", "L")),
        new(8, "Casual Shirt Dress", "casual-shirt-dress",
            "A belted shirt dress that works from day to evening.",
            1050m, false, "women-dresses", ["Beige", "Black"], Clothing("S", "L")),
        new(9, "Basic Ribbed Top", "basic-ribbed-top",
            "A fitted ribbed top, an easy layering essential.",
            300m, false, "women-tops", ["White", "Black", "Beige"], Clothing("XS", "L")),
        new(10, "Satin Blouse", "satin-blouse",
            "A smooth satin blouse with a relaxed fit.",
            650m, false, "women-tops", ["White", "Red"], Clothing("S", "L")),
        new(11, "Wide Leg Trousers", "wide-leg-trousers",
            "High-rise wide-leg trousers with a fluid drape.",
            950m, true, "women-pants", ["Black", "Beige"], Clothing("XS", "L")),
        new(12, "High Waist Jeans", "high-waist-jeans",
            "High-waisted jeans with a flattering straight fit.",
            1050m, false, "women-pants", ["Blue"], Clothing("XS", "XL")),
        new(13, "Everyday White Sneakers", "everyday-white-sneakers",
            "Clean white leather sneakers for everyday wear.",
            1600m, true, "shoes-sneakers", ["White"], Shoes("38", "45")),
        new(14, "Running Shoes", "running-shoes",
            "Lightweight cushioned running shoes.",
            1900m, false, "shoes-sneakers", ["Black", "Gray"], Shoes("39", "45")),
        new(15, "Leather Derby Shoes", "leather-derby-shoes",
            "Polished leather Derby shoes for formal occasions.",
            2200m, false, "shoes-formal", ["Black", "Brown"], Shoes("40", "45")),
        new(16, "Classic Loafers", "classic-loafers",
            "Slip-on leather loafers with a timeless look.",
            1800m, false, "shoes-formal", ["Brown", "Black"], Shoes("40", "44")),
        new(17, "Leather Sandals", "leather-sandals",
            "Comfortable leather sandals with adjustable straps.",
            700m, false, "shoes-sandals", ["Brown", "Black"], Shoes("38", "44")),
        new(18, "Everyday Slides", "everyday-slides",
            "Soft cushioned slides for home and beach.",
            400m, false, "shoes-sandals", ["Black", "White"], Shoes("38", "45"))
    ];

    /// <summary>Final stock values after demo-order deductions (spec.md "Special stock values").</summary>
    public static readonly IReadOnlyList<SpecialStockSeed> SpecialStock =
    [
        new("classic-cotton-t-shirt", "Black", "M", 1),
        new("classic-cotton-t-shirt", "White", "L", 0),
        new("floral-midi-dress", "Red", "S", 2),
        new("everyday-white-sneakers", "White", "42", 0),
        new("everyday-white-sneakers", "White", "43", 3),
        new("leather-derby-shoes", "Brown", "44", 1)
    ];

    private static string[] Clothing(string from, string to) => Range(ClothingSizes, from, to);

    private static string[] Shoes(string from, string to) => Range(ShoeSizes, from, to);

    private static string[] Range(string[] sizes, string from, string to)
    {
        var start = Array.IndexOf(sizes, from);
        var end = Array.IndexOf(sizes, to);
        return sizes[start..(end + 1)];
    }
}
