using Microsoft.AspNetCore.Mvc.ModelBinding;
using Store.Application.Products.Requests;

namespace Store.API.Binding;

/// <summary>
/// Binds <see cref="ProductSort"/> from its name only (case-insensitive). Numbers and
/// combinations are rejected as model errors (research R8).
/// </summary>
public sealed class ProductSortModelBinder : IModelBinder
{
    private static readonly string[] Names = Enum.GetNames<ProductSort>();

    public static readonly string ErrorMessage =
        $"Sort must be one of: {string.Join(", ", Names.Select(name => char.ToLowerInvariant(name[0]) + name[1..]))}.";

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        var name = Array.Find(Names, candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, ErrorMessage);
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(Enum.Parse<ProductSort>(name));
        return Task.CompletedTask;
    }
}

public sealed class ProductSortModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.ModelType == typeof(ProductSort) ? new ProductSortModelBinder() : null;
}
