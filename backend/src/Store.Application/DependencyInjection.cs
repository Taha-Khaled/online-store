using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Auth;
using Store.Application.Cart;
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
        services.AddScoped<AuthService>();
        services.AddScoped<SessionService>();
        services.AddScoped<CartReader>();

        return services;
    }
}
