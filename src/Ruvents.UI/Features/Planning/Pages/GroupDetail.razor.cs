using Microsoft.AspNetCore.Components;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class GroupDetail
{
    private const int PastPreviewCount = 6;

    private PlanningFamilyDetail? _group;
    private IReadOnlyList<PlanningEventSummary> _events = [];
    private IReadOnlyList<PlanningEventSummary> _upcoming = [];
    private IReadOnlyList<PlanningEventSummary> _past = [];
    private bool _showAllPast;
    private bool _zoneResolutionAttempted;
    private DateTimeOffset _now;
    private string? _message;
    private string _messageKind = "information";

    [Inject]
    private IViewerTimeZone ViewerTimeZone { get; set; } = default!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Parameter]
    public Guid GroupId { get; set; }

    private IEnumerable<PlanningEventSummary> VisiblePast =>
        _showAllPast ? _past : _past.Take(PastPreviewCount);

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        await LoadAsync(operation.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // A zone carried over from an earlier request is only provisional, so confirm it
        // once the renderer can ask the browser.
        if (IsDisposed || ViewerTimeZone.IsConfirmed || _zoneResolutionAttempted)
        {
            return;
        }

        _zoneResolutionAttempted = true;

        using var operation = CreateOperation();
        if (await ViewerTimeZone.ResolveAsync(operation.Token) is not null && !IsDisposed)
        {
            StateHasChanged();
        }
    }

    private static string Initial(string displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? "?" : displayName[..1].ToUpperInvariant();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _group = await Api.GetFamilyAsync(GroupId, cancellationToken);
        if (_group is null)
        {
            _events = [];
            _upcoming = [];
            _past = [];
            return;
        }

        _events = await Api.ListGroupEventsAsync(GroupId, cancellationToken);
        _now = TimeProvider.GetUtcNow();
        Partition();
    }

    private void Partition()
    {
        _upcoming = _events
            .Where(calendarEvent => StateOf(calendarEvent) is not EventTimeState.Past)
            .OrderBy(calendarEvent => calendarEvent.StartUtc)
            .ToList();
        _past = _events
            .Where(calendarEvent => StateOf(calendarEvent) is EventTimeState.Past)
            .OrderByDescending(calendarEvent => calendarEvent.StartUtc)
            .ToList();
    }

    private EventTimeState StateOf(PlanningEventSummary calendarEvent) =>
        EventDisplay.State(calendarEvent, _now);

    private bool IsHappeningNow(PlanningEventSummary calendarEvent) =>
        EventDisplay.IsHappeningNow(calendarEvent, _now);

    private string DisplayZoneId(PlanningEventSummary calendarEvent) =>
        EventDisplay.ZoneId(calendarEvent, ViewerTimeZone.ResolvedZoneId);

    private bool ShowsEventZone(PlanningEventSummary calendarEvent) =>
        EventDisplay.ShowsEventZone(calendarEvent, ViewerTimeZone.ResolvedZoneId);

    private void TogglePast() => _showAllPast = !_showAllPast;

    private async Task DecideAsync(Guid requestId, bool approve)
    {
        using var operation = CreateOperation();
        var result = await Api.DecideJoinRequestAsync(requestId, approve, operation.Token);
        _message = result switch
        {
            JoinDecisionResult.Decided => approve ? "Request approved." : "Request declined.",
            JoinDecisionResult.Forbidden => "Only organizers can decide join requests.",
            JoinDecisionResult.AlreadyDecided => "That request was already decided.",
            _ => "That request could not be found.",
        };
        _messageKind = result == JoinDecisionResult.Decided ? "success" : "error";
        await LoadAsync(operation.Token);
    }
}
