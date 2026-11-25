namespace Store.API.Tests.Fixtures;

/// <summary>Test clock that only moves when a test advances it (research R13).</summary>
public sealed class MutableTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset SeedTime = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly Lock _lock = new();
    private DateTimeOffset _now = SeedTime;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
        {
            _now = _now.Add(delta);
        }
    }
}
