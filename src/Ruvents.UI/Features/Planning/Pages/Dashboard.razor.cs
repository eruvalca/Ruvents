using Microsoft.AspNetCore.Components;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Dashboard
{
    private IReadOnlyList<PlanningEventSummary> _events = [];
    private IReadOnlyList<PlanningEventSummary> _upcoming = [];
    private IReadOnlyList<PlanningFamilyListItem> _groups = [];
    private int _unread;
    private bool _loading = true;
    private DateTimeOffset _now;

    [CascadingParameter(Name = "ViewerTimeZoneId")]
    public string? ViewerTimeZoneId { get; set; }

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _now = TimeProvider.GetUtcNow();
        // The directory drops finished events before it limits the result, so this is
        // only a guard for the boundary the server computed a moment earlier.
        _events = await Api.ListUpcomingEventsAsync(operation.Token);
        Apply();
        _groups = await Api.ListFamiliesAsync(operation.Token);
        _unread = await Api.UnreadCountAsync(operation.Token);
        _loading = false;
    }

    private void Apply() =>
        _upcoming = _events
            .Where(calendarEvent => StateOf(calendarEvent) is not EventTimeState.Past)
            .ToList();

    private EventTimeState StateOf(PlanningEventSummary calendarEvent) =>
        EventDisplay.State(calendarEvent, _now);

    private string DisplayZoneId(PlanningEventSummary calendarEvent) =>
        EventDisplay.ZoneId(calendarEvent, ViewerTimeZoneId);

    private bool ShowsEventZone(PlanningEventSummary calendarEvent) =>
        EventDisplay.ShowsEventZone(calendarEvent, ViewerTimeZoneId);
}
