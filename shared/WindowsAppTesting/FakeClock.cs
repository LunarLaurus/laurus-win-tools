namespace WindowsAppTesting;

public sealed class FakeClock : WindowsAppCore.IClock
{
    private DateTimeOffset _utcNow;

    public FakeClock(DateTimeOffset utcNow) => _utcNow = utcNow;
    public FakeClock() : this(DateTimeOffset.UtcNow) { }

    public DateTimeOffset UtcNow => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
    public void Set(DateTimeOffset value) => _utcNow = value;
}
