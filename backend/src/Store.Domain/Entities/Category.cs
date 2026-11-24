namespace Store.Domain.Entities;

public class Category
{
    public Category(string name, string slug, int? parentCategoryId)
    {
        Name = name;
        Slug = slug;
        ParentCategoryId = parentCategoryId;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public int? ParentCategoryId { get; private set; }

    public Category? ParentCategory { get; private set; }
    public ICollection<Category> ChildCategories { get; private set; } = new List<Category>();
}
