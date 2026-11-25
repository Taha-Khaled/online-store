namespace Store.Domain.Entities;

public class Color
{
    public Color(string name, string hexCode)
    {
        Name = name;
        HexCode = hexCode;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }
    public string HexCode { get; private set; }
}
