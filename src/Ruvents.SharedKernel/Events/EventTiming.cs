using System.Globalization;

namespace Ruvents.SharedKernel.Events;

/// <summary>
/// Formats event instants for display and reports where an event sits in time. All-day
/// dates are stored and displayed as UTC calendar dates, but the boundaries that decide
/// their state follow the event's own zone; timed events are displayed in the supplied
/// zone.
/// </summary>
public static class EventTiming
{
    public static string FormatRange(DateTimeOffset startUtc, DateTimeOffset? endUtc, bool isAllDay, string timeZoneId)
    {
        if (isAllDay)
        {
            // All-day ranges are stored at UTC midnight, so their calendar date must
            // stay in UTC. Converting to the event zone would shift the date backwards
            // for zones west of UTC and disagree with FormatDate.
            var startText = startUtc.UtcDateTime.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture);
            if (endUtc is null)
            {
                return startText;
            }

            var endDate = endUtc.Value.UtcDateTime;
            return string.Equals(startUtc.UtcDateTime.Date, endDate.Date)
                ? startText
                : $"{startText} – {endDate.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture)}";
        }

        var zone = ResolveTimeZone(timeZoneId);
        var start = TimeZoneInfo.ConvertTime(startUtc, zone);
        var result = start.ToString("ddd, MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
        if (endUtc is null)
        {
            return result;
        }

        // A multi-day timed event must show its end date; otherwise a June 1 to June 3
        // range reads as ending at 7 PM on June 1.
        var end = TimeZoneInfo.ConvertTime(endUtc.Value, zone);
        var endText = end.Date == start.Date
            ? end.ToString("h:mm tt", CultureInfo.CurrentCulture)
            : end.ToString("ddd, MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
        return $"{result} – {endText}";
    }

    public static EventTimeState GetTimeState(
        DateTimeOffset startUtc,
        DateTimeOffset? endUtc,
        bool isAllDay,
        string timeZoneId,
        DateTimeOffset nowUtc)
    {
        var zone = ResolveTimeZone(timeZoneId);

        // An all-day event's stored instant is the calendar date at UTC midnight, but the
        // date itself begins and ends in the event's own zone.
        var startsAtUtc = isAllDay ? StartOfDayUtc(startUtc.UtcDateTime.Date, zone) : startUtc;
        if (nowUtc < startsAtUtc)
        {
            return EventTimeState.Upcoming;
        }

        var endsAtUtc = isAllDay
            ? StartOfDayUtc((endUtc ?? startUtc).UtcDateTime.Date.AddDays(1), zone)
            : endUtc ?? EndOfStartDayUtc(startUtc, zone);

        return nowUtc >= endsAtUtc ? EventTimeState.Past : EventTimeState.InProgress;
    }

    private static DateTimeOffset EndOfStartDayUtc(DateTimeOffset startUtc, TimeZoneInfo zone)
    {
        // A timed event without an end stays current through the end of its start day;
        // otherwise it would read as past the moment it begins.
        var startLocal = TimeZoneInfo.ConvertTime(startUtc, zone);
        return StartOfDayUtc(startLocal.Date.AddDays(1), zone);
    }

    private static DateTimeOffset StartOfDayUtc(DateTime date, TimeZoneInfo zone)
    {
        var midnight = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);

        if (!zone.IsInvalidTime(midnight) && !zone.IsAmbiguousTime(midnight))
        {
            return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
        }

        // A daylight adjustment can skip midnight or repeat it. The offsets just before
        // and just after the boundary give the two possible instants for that wall time.
        var before = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight.AddHours(-3)));
        var after = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight.AddHours(3)));

        if (zone.IsInvalidTime(midnight))
        {
            // The clock jumps over midnight, so the date rolls over when the jump happens.
            return (before > after ? before : after).ToUniversalTime();
        }

        // Midnight happens twice; the date has already rolled over on the first one.
        return (before < after ? before : after).ToUniversalTime();
    }

    public static string FormatDate(DateTimeOffset instant, bool isAllDay, string timeZoneId) =>
        isAllDay
            ? instant.UtcDateTime.ToString("ddd, MMM d", CultureInfo.CurrentCulture)
            : TimeZoneInfo.ConvertTime(instant, ResolveTimeZone(timeZoneId)).ToString("ddd, MMM d h:mm tt", CultureInfo.CurrentCulture);

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
