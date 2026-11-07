using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;

namespace Ruvents.UI.Planning;

/// <summary>
/// Chooses the zone an event's times are shown in and derives what that means for the
/// viewer. Times are formatted in the viewer's zone when it is known, while the event's
/// own zone decides where it sits in time so every surface agrees.
/// </summary>
internal static class EventDisplay
{
    public static string ZoneId(PlanningEventSummary summary, string? viewerZoneId) =>
        string.IsNullOrWhiteSpace(viewerZoneId) ? summary.TimeZoneId : viewerZoneId;

    public static EventTimeState State(PlanningEventSummary summary, DateTimeOffset nowUtc) =>
        // Where an event sits in time is always judged in the event's own zone, so a
        // statically rendered list and an interactively formatted one agree on the state.
        EventTiming.GetTimeState(
            summary.StartUtc,
            summary.EndUtc,
            summary.IsAllDay,
            summary.TimeZoneId,
            nowUtc);

    public static bool ShowsEventZone(PlanningEventSummary summary, string? viewerZoneId) =>
        !summary.IsAllDay
        && !string.IsNullOrWhiteSpace(viewerZoneId)
        && !string.Equals(viewerZoneId, summary.TimeZoneId, StringComparison.OrdinalIgnoreCase);
}
