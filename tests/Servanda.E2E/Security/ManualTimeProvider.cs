namespace Servanda.E2E.Security;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void Advance(TimeSpan duration)
    {
        _utcNow += duration;
    }
}
