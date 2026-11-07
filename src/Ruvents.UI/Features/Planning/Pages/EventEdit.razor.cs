using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class EventEdit
{
    private IReadOnlyList<PlanningFamilyListItem> _groups = [];
    private EventModel _model = new();
    private IReadOnlyList<TimeZoneInfo> _zones = [];
    private IBrowserFile? _cover;
    private Guid? _selectedGroupId;
    private string? _selectedGroupName;
    private string? _message;
    private string _messageKind = "information";

    [Parameter]
    public Guid? EventId { get; set; }

    // Bound as text so a malformed link shows a message instead of failing the request.
    [SupplyParameterFromQuery(Name = "groupId")]
    private string? GroupIdText { get; set; }

    private Guid? RequestedGroupId => Guid.TryParse(GroupIdText, out var groupId) ? groupId : null;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Inject]
    private IViewerTimeZone ViewerTimeZone { get; set; } = default!;

    private bool ViewerZoneDiffers =>
        ViewerTimeZone.ResolvedZoneId is { } zoneId
        && !string.Equals(zoneId, _model.TimeZoneId, StringComparison.OrdinalIgnoreCase);

    // The form is only touched by the zone fix-up once initialization has supplied a
    // model, and only while its values are still the server-side fallback.
    private bool _modelReady;
    private bool _zoneResolutionAttempted;
    private string? _fallbackZoneId;
    private DateTime _fallbackStart;
    private DateTime? _fallbackEnd;

    private bool DefaultsAreUntouched =>
        string.Equals(_model.TimeZoneId, _fallbackZoneId, StringComparison.Ordinal)
        && _model.StartLocal == _fallbackStart
        && _model.EndLocal == _fallbackEnd;

    protected override async Task OnInitializedAsync()
    {
        _zones = TimeZoneInfo.GetSystemTimeZones();
        using var operation = CreateOperation();

        if (EventId is not null)
        {
            var detail = await Api.GetEventAsync(EventId.Value, operation.Token);
            if (detail is null || !detail.CanManage)
            {
                Report("You cannot edit this event.", "error");
                return;
            }

            _selectedGroupId = detail.FamilyGroupId;
            _selectedGroupName = detail.GroupName;
            var zone = ResolveZone(detail.TimeZoneId);
            _model = new EventModel
            {
                Title = detail.Title,
                Description = detail.Description,
                LocationName = detail.LocationName,
                LocationAddress = detail.LocationAddress,
                MapLink = detail.MapLink,
                TimeZoneId = detail.TimeZoneId,
                IsAllDay = detail.IsAllDay,
                StartLocal = TimeZoneInfo.ConvertTime(detail.StartUtc, zone).DateTime,
                EndLocal = detail.EndUtc is null ? null : TimeZoneInfo.ConvertTime(detail.EndUtc.Value, zone).DateTime,
                RsvpDeadlineLocal = detail.RsvpDeadlineUtc is null ? null : TimeZoneInfo.ConvertTime(detail.RsvpDeadlineUtc.Value, zone).DateTime,
            };
            _modelReady = true;
            return;
        }

        _groups = await Api.ListFamiliesAsync(operation.Token);
        if (_groups.Count == 0)
        {
            Report("You are not in a family group yet.", "error");
            return;
        }

        // When the caller named a group, honour it. Otherwise a person in exactly one
        // group is already decided; only a multi-group member has to choose.
        var requested = RequestedGroupId;
        Guid? selected = null;
        if (requested is not null && _groups.Any(group => group.Id == requested))
        {
            selected = requested;
        }
        else if (_groups.Count == 1)
        {
            selected = _groups[0].Id;
        }

        _selectedGroupId = selected;
        _selectedGroupName = _groups.FirstOrDefault(group => group.Id == _selectedGroupId)?.Name;

        // A page reached after the browser zone is known uses it directly; otherwise this
        // is the server-side fallback that the first interactive render replaces.
        var resolvedZoneId = ViewerTimeZone.ResolvedZoneId;
        _fallbackZoneId = resolvedZoneId is null ? TimeZoneInfo.Local.Id : null;
        ApplyDefaultTimes(ResolveZone(resolvedZoneId ?? TimeZoneInfo.Local.Id));
        _fallbackStart = _model.StartLocal;
        _fallbackEnd = _model.EndLocal;
        _modelReady = true;
    }

    private void ApplyDefaultTimes(TimeZoneInfo zone)
    {
        _model.TimeZoneId = zone.Id;
        _model.StartLocal = TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), zone).DateTime.Date.AddDays(1).AddHours(18);
        _model.EndLocal = _model.StartLocal.AddMinutes(30);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The first render can complete while OnInitializedAsync is still awaiting, so
        // wait for the model before touching it. Later renders retry because the model
        // completing triggers one.
        if (IsDisposed || !_modelReady || _zoneResolutionAttempted)
        {
            return;
        }

        _zoneResolutionAttempted = true;

        using var operation = CreateOperation();
        if (ViewerTimeZone.ResolvedZoneId is null)
        {
            await ViewerTimeZone.ResolveAsync(operation.Token);
        }

        if (IsDisposed || ViewerTimeZone.ResolvedZoneId is not { } zoneId)
        {
            return;
        }

        // Editing keeps the event's own zone because the wall-clock fields are entered
        // in that zone; only a new event adopts the browser zone.
        if (EventId is null && _fallbackZoneId is not null && DefaultsAreUntouched)
        {
            ApplyDefaultTimes(ResolveZone(zoneId));
        }

        _fallbackZoneId = null;
        StateHasChanged();
    }

    private void OnStartChanged()
    {
        // Keep the end sensible: 30 minutes after the start whenever the current end
        // is missing or no longer after the start.
        if (_model.EndLocal is null || _model.EndLocal <= _model.StartLocal)
        {
            _model.EndLocal = _model.StartLocal.AddMinutes(30);
        }
    }

    private async Task OnCoverSelectedAsync(InputFileChangeEventArgs args)
    {
        _cover = args.File;
        await Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (_selectedGroupId is null)
        {
            Report("Choose a family group for this event.", "error");
            return;
        }

        if (_model.EndLocal is not null && _model.EndLocal <= _model.StartLocal)
        {
            Report("The end time must be after the start time.", "error");
            return;
        }

        using var operation = CreateOperation();
        var zone = ResolveZone(_model.TimeZoneId);
        var input = new PlanningEventInput(
            _selectedGroupId.Value,
            _model.Title.Trim(),
            _model.Description,
            _model.LocationName.Trim(),
            _model.LocationAddress,
            _model.MapLink,
            ToUtc(_model.StartLocal, zone, _model.IsAllDay),
            _model.EndLocal is null ? null : ToUtc(_model.EndLocal.Value, zone, _model.IsAllDay),
            _model.IsAllDay,
            zone.Id,
            _model.RsvpDeadlineLocal is null ? null : ToUtc(_model.RsvpDeadlineLocal.Value, zone, allDay: false));

        Guid? savedId;
        if (EventId is not null)
        {
            var updated = await Api.UpdateEventAsync(EventId.Value, input, operation.Token);
            savedId = updated ? EventId : null;
            if (!updated)
            {
                Report("You cannot edit this event.", "error");
            }
        }
        else
        {
            savedId = await Api.CreateEventAsync(input, operation.Token);
            if (savedId is null)
            {
                Report("You cannot add events to that group.", "error");
            }
        }

        if (savedId is null)
        {
            return;
        }

        if (_cover is not null)
        {
            await using var stream = _cover.OpenReadStream(5 * 1024 * 1024, operation.Token);
            await Api.UploadEventCoverAsync(savedId.Value, stream, _cover.ContentType, operation.Token);
        }

        Navigation.NavigateTo($"events/{savedId.Value}");
    }

    private static TimeZoneInfo ResolveZone(string timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone, bool allDay)
    {
        if (allDay)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(local.Date, DateTimeKind.Utc));
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private void Report(string message, string kind)
    {
        _message = message;
        _messageKind = kind;
    }

    private sealed class EventModel
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string LocationName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? LocationAddress { get; set; }

        [StringLength(2000)]
        public string? MapLink { get; set; }

        public string TimeZoneId { get; set; } = "UTC";

        [Required]
        public DateTime StartLocal { get; set; }

        public DateTime? EndLocal { get; set; }

        public bool IsAllDay { get; set; }

        public DateTime? RsvpDeadlineLocal { get; set; }
    }
}
