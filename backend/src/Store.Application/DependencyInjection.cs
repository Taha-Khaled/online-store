using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Categories;
using Store.Application.Products;

namespace Store.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();

        return services;
    }
}
