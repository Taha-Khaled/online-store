using Shouldly;
using Store.Infrastructure.Persistence.Converters;

namespace Store.Infrastructure.Tests.Persistence;

public class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();

    [Fact]
    public void ConvertFromProvider_MarksUnspecifiedValueAsUtcWithoutShifting()
    {
        var stored = new DateTime(2026, 11, 24, 10, 15, 0, DateTimeKind.Unspecified);

        var result = (DateTime)_converter.ConvertFromProvider(stored)!;

        result.Kind.ShouldBe(DateTimeKind.Utc);
        result.Ticks.ShouldBe(stored.Ticks);
    }

    [Fact]
    public void ConvertToProvider_StoresValueUnchanged()
    {
        var utc = new DateTime(2026, 11, 24, 10, 15, 0, DateTimeKind.Utc);

        var result = (DateTime)_converter.ConvertToProvider(utc)!;

        result.ShouldBe(utc);
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
