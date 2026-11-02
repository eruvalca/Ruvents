namespace Ruvents.IntegrationTests.Support;

/// <summary>A controllable clock so scenario tests can cross RSVP deadlines.</summary>
internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan duration) => Now = Now.Add(duration);
}
