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

    [Inject]
    private IViewerTimeZone ViewerTimeZone { get; set; } = default!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _now = TimeProvider.GetUtcNow();
        // The directory keeps events that started yesterday so multi-day events stay
        // visible, so anything already finished is dropped here.
        _events = await Api.ListUpcomingEventsAsync(operation.Token);
        Apply();
        _groups = await Api.ListFamiliesAsync(operation.Token);
        _unread = await Api.UnreadCountAsync(operation.Token);
        _loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || IsDisposed || ViewerTimeZone.ResolvedZoneId is not null)
        {
            return;
        }

        using var operation = CreateOperation();
        if (await ViewerTimeZone.ResolveAsync(operation.Token) is not null && !IsDisposed)
        {
            Apply();
            StateHasChanged();
        }
    }

    private void Apply() =>
        _upcoming = _events
            .Where(calendarEvent => StateOf(calendarEvent) is not EventTimeState.Past)
            .ToList();

    private EventTimeState StateOf(PlanningEventSummary calendarEvent) =>
        EventDisplay.State(calendarEvent, ViewerTimeZone.ResolvedZoneId, _now);

    private string DisplayZoneId(PlanningEventSummary calendarEvent) =>
        EventDisplay.ZoneId(calendarEvent, ViewerTimeZone.ResolvedZoneId);

    private bool ShowsEventZone(PlanningEventSummary calendarEvent) =>
        EventDisplay.ShowsEventZone(calendarEvent, ViewerTimeZone.ResolvedZoneId);
}
