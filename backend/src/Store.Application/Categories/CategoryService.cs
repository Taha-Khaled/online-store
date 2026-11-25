using Microsoft.EntityFrameworkCore;
using Store.Application.Common;

namespace Store.Application.Categories;

/// <summary>Public category tree (FR-031).</summary>
public sealed class CategoryService(IAppDbContext db)
{
    public async Task<IReadOnlyList<CategoryResponse>> GetTreeAsync(CancellationToken cancellationToken = default) =>
        await db.Categories
            .AsNoTracking()
            .Where(category => category.ParentCategoryId == null)
            .OrderBy(category => category.Id)
            .Select(category => new CategoryResponse(
                category.Name,
                category.Slug,
                category.ChildCategories
                    .OrderBy(child => child.Id)
                    .Select(child => new CategoryChildResponse(child.Name, child.Slug))
                    .ToList()))
            .ToListAsync(cancellationToken);
}
