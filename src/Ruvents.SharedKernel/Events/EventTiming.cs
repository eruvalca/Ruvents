using System.Globalization;

namespace Ruvents.SharedKernel.Events;

/// <summary>
/// Formats event instants for display and reports where an event sits in time. All-day
/// events keep their calendar date in UTC; timed events are shown in the supplied zone.
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
        if (nowUtc < startUtc)
        {
            return EventTimeState.Upcoming;
        }

        return nowUtc >= GetExclusiveEndUtc(startUtc, endUtc, isAllDay, timeZoneId)
            ? EventTimeState.Past
            : EventTimeState.InProgress;
    }

    private static DateTimeOffset GetExclusiveEndUtc(
        DateTimeOffset startUtc,
        DateTimeOffset? endUtc,
        bool isAllDay,
        string timeZoneId)
    {
        if (isAllDay)
        {
            // All-day instants are stored at UTC midnight and displayed as calendar
            // dates, so the stored end date is the last day the event covers.
            var lastDate = (endUtc ?? startUtc).UtcDateTime.Date;
            return new DateTimeOffset(lastDate.AddDays(1), TimeSpan.Zero);
        }

        if (endUtc is not null)
        {
            return endUtc.Value;
        }

        // A timed event without an end stays current through the end of its start day;
        // otherwise it would read as past the moment it begins.
        var zone = ResolveTimeZone(timeZoneId);
        var startLocal = TimeZoneInfo.ConvertTime(startUtc, zone);
        var nextMidnight = startLocal.Date.AddDays(1);
        return new DateTimeOffset(nextMidnight, zone.GetUtcOffset(nextMidnight)).ToUniversalTime();
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
