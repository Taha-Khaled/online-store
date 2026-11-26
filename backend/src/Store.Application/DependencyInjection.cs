using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Auth.Services;
using Store.Application.Cart.Services;
using Store.Application.Categories.Services;
using Store.Application.Orders.Services;
using Store.Application.Products.Services;

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
        services.AddScoped<CartService>();
        services.AddScoped<CartMergeService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<CustomerOrderService>();

        return services;
    }
}
