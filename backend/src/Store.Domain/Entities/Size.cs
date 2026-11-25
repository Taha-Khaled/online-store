namespace Store.Domain.Entities;

public class Size
{
    public Size(string name, int displayOrder)
    {
        Name = name;
        DisplayOrder = displayOrder;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }
    public int DisplayOrder { get; private set; }
}
