namespace Store.Application.Products;

/// <summary>The only sort orders the product list accepts (FR-036, constitution III).</summary>
public enum ProductSort
{
    Newest,
    PriceAsc,
    PriceDesc,
    NameAsc
}
