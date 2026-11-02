using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Ruvents.SharedKernel.Events;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Events;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class EventTimingTests : IDisposable
{
    // A zone west of UTC, where UTC midnight falls on the previous calendar date.
    private const string WesternZone = "America/New_York";

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public EventTimingTests() => CultureInfo.CurrentCulture = new CultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _originalCulture;

    [Fact]
    public void AllDayRangeKeepsItsUtcCalendarDateInsteadOfShiftingIntoTheEventZone()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var text = EventTiming.FormatRange(start, null, isAllDay: true, WesternZone);

        text.ShouldContain("Jun 1, 2026");
        text.ShouldNotContain("May 31");
        // The dashboard and group list format the same UTC date, so both views agree.
        EventTiming.FormatDate(start, isAllDay: true, WesternZone).ShouldContain("Jun 1");
    }

    [Fact]
    public void AllDayRangeCollapsesASingleDateAndListsAMultiDayRange()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        EventTiming.FormatRange(start, start.AddDays(1).AddTicks(-1), isAllDay: true, WesternZone)
            .ShouldBe(EventTiming.FormatRange(start, null, isAllDay: true, WesternZone));

        EventTiming.FormatRange(start, start.AddDays(2), isAllDay: true, WesternZone)
            .ShouldContain("Jun 3, 2026");
    }

    [Fact]
    public void TimedRangeIsShownInTheEventTimeZone()
    {
        // 2026-06-01 18:00 UTC is 14:00 in New York during daylight saving time.
        var start = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        var text = EventTiming.FormatRange(start, start.AddHours(1), isAllDay: false, WesternZone);

        text.ShouldContain("Jun 1, 2026");
        text.ShouldContain("2:00 PM");
        text.ShouldContain("3:00 PM");
    }

    [Fact]
    public void TimedRangeThatEndsOnAnotherDayShowsTheEndDate()
    {
        var start = new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero);

        // 22:00 UTC on 1 June is 18:00 in New York; three days later is 3 June there.
        var text = EventTiming.FormatRange(start, start.AddDays(3).AddHours(1), isAllDay: false, WesternZone);

        text.ShouldContain("Jun 1, 2026");
        text.ShouldContain("6:00 PM");
        text.ShouldContain("Jun 4, 2026");
        text.ShouldContain("7:00 PM");
    }
}
