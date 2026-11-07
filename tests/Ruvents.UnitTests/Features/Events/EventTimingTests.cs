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

    [Fact]
    public void TimedEventMovesFromUpcomingToInProgressToPast()
    {
        var start = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);

        EventTiming.GetTimeState(start, end, isAllDay: false, WesternZone, start.AddMinutes(-1))
            .ShouldBe(EventTimeState.Upcoming);
        EventTiming.GetTimeState(start, end, isAllDay: false, WesternZone, start.AddMinutes(30))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, end, isAllDay: false, WesternZone, end)
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void AllDayEventStaysCurrentThroughItsUtcCalendarDate()
    {
        // Stored at UTC midnight, so a zone west of UTC must not end it early.
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, null, isAllDay: true, WesternZone, start.AddHours(-1))
            .ShouldBe(EventTimeState.Upcoming);
        EventTiming.GetTimeState(start, null, isAllDay: true, WesternZone, start.AddHours(23))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, null, isAllDay: true, WesternZone, start.AddDays(1))
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void AllDayEventKeepsItsStoredEndDateAsTheLastDayItCovers()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, end, isAllDay: true, WesternZone, end.AddHours(12))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, end, isAllDay: true, WesternZone, end.AddDays(1))
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void TimedEventWithoutAnEndStaysCurrentThroughItsStartDay()
    {
        // 22:00 UTC on 1 June is 18:00 in New York, so the event covers the rest of
        // that local day rather than ending the moment it starts.
        var start = new DateTimeOffset(2026, 6, 1, 22, 0, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, null, isAllDay: false, WesternZone, start.AddHours(5))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, null, isAllDay: false, WesternZone, start.AddHours(7))
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void TimedEventWithoutAnEndCanStayCurrentForMoreThanTwentyFourHours()
    {
        // 1 November 2026 is a fall-back day in New York, so the local day lasts 25 hours
        // and an event starting just after midnight is still current more than 24 hours
        // later. Candidate queries must therefore widen their window past one day.
        var start = new DateTimeOffset(2026, 11, 1, 4, 30, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, null, isAllDay: false, WesternZone, start.AddHours(24).AddMinutes(15))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, null, isAllDay: false, WesternZone, start.AddHours(24).AddMinutes(45))
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void TimedEventWithoutAnEndHandlesAMidnightThatDoesNotExist()
    {
        // Chile moves clocks forward at midnight, so 11 September 2022 begins at 01:00
        // and the previous day ends when the jump happens, at 04:00 UTC.
        var start = new DateTimeOffset(2022, 9, 10, 20, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2022, 9, 11, 4, 0, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, null, isAllDay: false, "America/Santiago", end.AddSeconds(-1))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, null, isAllDay: false, "America/Santiago", end)
            .ShouldBe(EventTimeState.Past);
    }

    [Fact]
    public void TimedEventWithoutAnEndHandlesAMidnightThatHappensTwice()
    {
        // Cuba moves clocks back at 01:00, so 6 November 2022 has two 00:00 hours and
        // only the first one ends the previous day, at 04:00 UTC.
        var start = new DateTimeOffset(2022, 11, 5, 18, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2022, 11, 6, 4, 0, 0, TimeSpan.Zero);

        EventTiming.GetTimeState(start, null, isAllDay: false, "America/Havana", end.AddSeconds(-1))
            .ShouldBe(EventTimeState.InProgress);
        EventTiming.GetTimeState(start, null, isAllDay: false, "America/Havana", end)
            .ShouldBe(EventTimeState.Past);
    }
}
