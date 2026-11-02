using System.Globalization;

namespace Ruvents.SharedKernel.Events;

/// <summary>
/// Formats event instants for display. All-day events keep their calendar date in UTC;
/// timed events are shown in the event's own time zone.
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
        return endUtc is null
            ? result
            : $"{result} – {TimeZoneInfo.ConvertTime(endUtc.Value, zone).ToString("h:mm tt", CultureInfo.CurrentCulture)}";
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
