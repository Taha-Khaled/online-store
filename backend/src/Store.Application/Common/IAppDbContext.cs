using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Store.Domain.Entities;

namespace Store.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Color> Colors { get; }
    DbSet<Size> Sizes { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
