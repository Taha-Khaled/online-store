namespace Store.Infrastructure.Tests.Fixtures;

/// <summary>A clock that always returns the same UTC time (research R6).</summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
