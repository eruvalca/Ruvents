using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;

namespace Ruvents.UI.Planning;

/// <summary>
/// Chooses the zone an event's times are shown in and derives what that means for the
/// viewer. The browser zone is unknown until the renderer connects, so the event's own
/// zone stands in until then.
/// </summary>
internal static class EventDisplay
{
    public static string ZoneId(PlanningEventSummary summary, string? viewerZoneId) =>
        string.IsNullOrWhiteSpace(viewerZoneId) ? summary.TimeZoneId : viewerZoneId;

    public static EventTimeState State(PlanningEventSummary summary, string? viewerZoneId, DateTimeOffset nowUtc) =>
        EventTiming.GetTimeState(
            summary.StartUtc,
            summary.EndUtc,
            summary.IsAllDay,
            ZoneId(summary, viewerZoneId),
            nowUtc);

    public static bool ShowsEventZone(PlanningEventSummary summary, string? viewerZoneId) =>
        !summary.IsAllDay
        && !string.IsNullOrWhiteSpace(viewerZoneId)
        && !string.Equals(viewerZoneId, summary.TimeZoneId, StringComparison.OrdinalIgnoreCase);
}
