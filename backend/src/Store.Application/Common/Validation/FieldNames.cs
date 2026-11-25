using System.Text.Json;

namespace Store.Application.Common.Validation;

public static class FieldNames
{
    /// <summary>Converts a property path such as <c>Items.ProductName</c> to <c>items.productName</c>.</summary>
    public static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
