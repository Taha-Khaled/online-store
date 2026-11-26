namespace Store.Domain;

/// <summary>Store-wide limits used by domain entities and application services.</summary>
public static class StoreLimits
{
    /// <summary>Maximum units of one variant per cart line and per order (constitution Business Rules).</summary>
    public const int MaxQuantityPerVariant = 5;

    /// <summary>Maximum different variants in one cart.</summary>
    public const int MaxCartLines = 30;
}
