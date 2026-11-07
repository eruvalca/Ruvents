using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class EventDetail
{
    private PlanningEventDetail? _event;
    private PlanningRsvpState _rsvp = new(null, null, [], [], false);
    private IReadOnlyList<PlanningRsvpEntry> _attendees = [];
    private IReadOnlyList<PlanningEventPost> _posts = [];
    private IReadOnlyList<PlanningHouseholdMember> _household = [];
    private PlanningPotluckBoard? _board;
    private IReadOnlyList<PlanningFamilyMember> _members = [];
    private readonly Dictionary<Guid, int> _claimQuantity = [];
    private readonly Dictionary<Guid, string?> _claimNote = [];
    private readonly Dictionary<Guid, int> _assignQuantity = [];
    private readonly Dictionary<Guid, string> _assignUser = [];
    private Guid? _editingSlotId;
    private string _editName = string.Empty;
    private string? _editDescription;
    private int _editQuantity = 1;
    private readonly HashSet<Guid> _attendingHousehold = [];
    private bool _rsvpAttending;
    private string? _rsvpNote;
    private string _guestsText = string.Empty;
    private string? _message;
    private string _messageKind = "information";
    private string? _announcement;
    private string? _comment;
    private string? _reply;
    private string _customItem = string.Empty;
    private string _newSlotName = string.Empty;
    private int _newSlotQuantity = 1;
    private DateTimeOffset _now;

    [Inject]
    private IViewerTimeZone ViewerTimeZone { get; set; } = default!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = default!;

    private string DisplayZoneId => ViewerTimeZone.ResolvedZoneId ?? _event?.TimeZoneId ?? "UTC";

    private EventTimeState TimeState =>
        _event is null
            ? EventTimeState.Upcoming
            : EventTiming.GetTimeState(_event.StartUtc, _event.EndUtc, _event.IsAllDay, DisplayZoneId, _now);

    private bool ShowsEventZone =>
        _event is not null
        && !_event.IsAllDay
        && ViewerTimeZone.ResolvedZoneId is { } zoneId
        && !string.Equals(zoneId, _event.TimeZoneId, StringComparison.OrdinalIgnoreCase);

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        await LoadAsync(operation.Token);
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
            StateHasChanged();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _now = TimeProvider.GetUtcNow();
        _event = await Api.GetEventAsync(EventId, cancellationToken);
        if (_event is null)
        {
            return;
        }

        _rsvp = await Api.GetRsvpAsync(EventId, cancellationToken);
        _attendees = await Api.ListRsvpsAsync(EventId, cancellationToken);
        _posts = await Api.ListPostsAsync(EventId, cancellationToken);
        _household = await Api.ListHouseholdAsync(cancellationToken);
        _board = await Api.GetPotluckAsync(EventId, cancellationToken);
        var group = await Api.GetFamilyAsync(_event.FamilyGroupId, cancellationToken);
        _members = group?.Members ?? [];
        SeedSlotFormState();

        _rsvpAttending = _rsvp.IsAttending ?? false;
        _rsvpNote = _rsvp.Note;
        _guestsText = string.Join(Environment.NewLine, _rsvp.Guests);
        _attendingHousehold.Clear();
        foreach (var id in _rsvp.AttendingHouseholdMemberIds)
        {
            _attendingHousehold.Add(id);
        }
    }

    private void SeedSlotFormState()
    {
        if (_board is null)
        {
            return;
        }

        foreach (var slot in _board.Slots)
        {
            var remaining = Math.Max(1, slot.QuantityNeeded - slot.QuantityClaimed);
            if (!_claimQuantity.TryGetValue(slot.Id, out var current))
            {
                _claimQuantity[slot.Id] = 1;
            }
            else if (current > remaining)
            {
                _claimQuantity[slot.Id] = remaining;
            }

            _claimNote.TryAdd(slot.Id, null);
            if (!_assignQuantity.ContainsKey(slot.Id))
            {
                _assignQuantity[slot.Id] = 1;
            }

            _assignUser.TryAdd(slot.Id, _members.Count > 0 ? _members[0].UserId : string.Empty);
        }
    }

    private void StartSlotEdit(PlanningPotluckSlot slot)
    {
        _editingSlotId = slot.Id;
        _editName = slot.Name;
        _editDescription = slot.Description;
        _editQuantity = slot.QuantityNeeded;
    }

    private void CancelSlotEdit() => _editingSlotId = null;

    private async Task SaveSlotAsync(Guid slotId)
    {
        using var operation = CreateOperation();
        var result = await Api.UpdatePotluckSlotAsync(slotId, _editName, _editDescription, _editQuantity, operation.Token);
        if (result == PotluckResult.Ok)
        {
            _editingSlotId = null;
            Report("Slot updated.", "success");
        }
        else
        {
            Report("Only the creator or an organizer can change slots.", "error");
        }

        await LoadAsync(operation.Token);
    }

    private async Task RemoveClaimAsync(Guid slotId, string claimUserId)
    {
        using var operation = CreateOperation();
        var result = await Api.RemovePotluckClaimAsync(slotId, claimUserId, operation.Token);
        Report(
            result == PotluckResult.Ok ? "Claim removed." : "Only the creator or an organizer can remove that claim.",
            result == PotluckResult.Ok ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private void ToggleHousehold(Guid householdMemberId, object? value)
    {
        var attending = value is bool flag && flag;
        if (attending)
        {
            _attendingHousehold.Add(householdMemberId);
        }
        else
        {
            _attendingHousehold.Remove(householdMemberId);
        }
    }

    private async Task SaveRsvpAsync()
    {
        using var operation = CreateOperation();
        var guests = _guestsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await Api.SaveRsvpAsync(EventId, _rsvpAttending, _rsvpNote, _attendingHousehold, guests, operation.Token);
        Report(
            result switch
            {
                RsvpSaveResult.Saved => "Your RSVP was saved.",
                RsvpSaveResult.Locked => "RSVP is closed for this event.",
                RsvpSaveResult.InvalidHouseholdMember => "One of those household members is not yours.",
                _ => "You cannot respond to this event.",
            },
            result == RsvpSaveResult.Saved ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task ToggleCancelledAsync()
    {
        if (_event is null)
        {
            return;
        }

        using var operation = CreateOperation();
        var cancelled = _event.Status != EventStatus.Cancelled;
        var updated = await Api.SetEventStatusAsync(EventId, cancelled, operation.Token);
        Report(updated ? "Event updated." : "Only the creator or an organizer can change this event.", updated ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task UploadCoverAsync(InputFileChangeEventArgs args)
    {
        using var operation = CreateOperation();
        var file = args.File;
        await using var stream = file.OpenReadStream(5 * 1024 * 1024, operation.Token);
        var saved = await Api.UploadEventCoverAsync(EventId, stream, file.ContentType, operation.Token);
        Report(saved ? "Cover image updated." : "Only the creator or an organizer can change this event.", saved ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task SetPotluckAsync(bool enabled)
    {
        using var operation = CreateOperation();
        var result = await Api.SetPotluckEnabledAsync(EventId, enabled, operation.Token);
        if (result != PotluckResult.Ok)
        {
            Report("Only the creator or an organizer can change potluck.", "error");
        }
        else
        {
            Report(enabled ? "Potluck sign-up opened." : "Potluck sign-up closed.", "success");
        }
        await LoadAsync(operation.Token);
    }

    private async Task ClaimSlotAsync(Guid slotId, int remaining)
    {
        using var operation = CreateOperation();
        var requested = _claimQuantity.TryGetValue(slotId, out var value) ? value : 1;
        var quantity = Math.Clamp(requested, 1, Math.Max(1, remaining));
        var note = _claimNote.TryGetValue(slotId, out var text) ? text : null;
        var result = await Api.ClaimPotluckSlotAsync(slotId, quantity, note, operation.Token);
        Report(
            result switch
            {
                PotluckResult.Ok => "Your potluck claim was saved.",
                PotluckResult.Full => "That slot is already fully claimed.",
                PotluckResult.Conflict => "Someone else claimed at the same time. Try again.",
                _ => "You cannot claim that slot.",
            },
            result == PotluckResult.Ok ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task UnclaimAsync(Guid slotId)
    {
        using var operation = CreateOperation();
        var result = await Api.UnclaimPotluckSlotAsync(slotId, operation.Token);
        Report(result == PotluckResult.Ok ? "Your claim was removed." : "That claim could not be removed.", result == PotluckResult.Ok ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task AssignAsync(Guid slotId)
    {
        using var operation = CreateOperation();
        var assignee = _assignUser.TryGetValue(slotId, out var user) ? user : string.Empty;
        var quantity = _assignQuantity.TryGetValue(slotId, out var value) ? value : 1;
        if (string.IsNullOrEmpty(assignee))
        {
            Report("Choose a member to assign.", "error");
            return;
        }

        var result = await Api.AssignPotluckSlotAsync(slotId, assignee, quantity, operation.Token);
        Report(
            result switch
            {
                PotluckResult.Ok => "Slot assigned.",
                PotluckResult.Full => "That slot is already fully claimed.",
                _ => "Only the creator or an organizer can assign slots.",
            },
            result == PotluckResult.Ok ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task RemoveSlotAsync(Guid slotId)
    {
        using var operation = CreateOperation();
        var result = await Api.RemovePotluckSlotAsync(slotId, operation.Token);
        Report(result == PotluckResult.Ok ? "Slot removed." : "Only the creator or an organizer can remove slots.", result == PotluckResult.Ok ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private async Task AddSlotAsync()
    {
        using var operation = CreateOperation();
        var name = _newSlotName.Trim();
        if (name.Length is < 1 or > 120)
        {
            Report("Enter a slot name up to 120 characters.", "error");
            return;
        }

        var result = await Api.AddPotluckSlotAsync(EventId, name, null, _newSlotQuantity, operation.Token);
        Report(result == PotluckResult.Ok ? "Slot added." : "Only the creator or an organizer can add slots.", result == PotluckResult.Ok ? "success" : "error");
        _newSlotName = string.Empty;
        await LoadAsync(operation.Token);
    }

    private async Task AddCustomItemAsync()
    {
        using var operation = CreateOperation();
        var name = _customItem.Trim();
        if (name.Length is < 1 or > 120)
        {
            Report("Enter an item name up to 120 characters.", "error");
            return;
        }

        var result = await Api.AddCustomPotluckItemAsync(EventId, name, 1, null, operation.Token);
        Report(result == PotluckResult.Ok ? "Item added." : "You cannot add items to this potluck.", result == PotluckResult.Ok ? "success" : "error");
        _customItem = string.Empty;
        await LoadAsync(operation.Token);
    }

    private async Task PostAsync(bool announcement, Guid? parentPostId)
    {
        using var operation = CreateOperation();
        string? body;
        if (parentPostId is not null)
        {
            body = _reply;
        }
        else
        {
            body = announcement ? _announcement : _comment;
        }
        var result = await Api.AddPostAsync(EventId, announcement, body ?? string.Empty, parentPostId, operation.Token);
        Report(
            result switch
            {
                PostAddResult.Added => announcement ? "Announcement posted." : "Comment posted.",
                PostAddResult.Empty => "Write something before posting.",
                PostAddResult.InvalidParent => "You can only reply to a top-level comment or announcement.",
                _ => "Only the creator or an organizer can post announcements.",
            },
            result == PostAddResult.Added ? "success" : "error");

        if (result == PostAddResult.Added)
        {
            _announcement = null;
            _comment = null;
            _reply = null;
        }

        await LoadAsync(operation.Token);
    }

    private async Task DeletePostAsync(Guid postId)
    {
        using var operation = CreateOperation();
        var deleted = await Api.DeletePostAsync(postId, operation.Token);
        Report(deleted ? "Post deleted." : "That post could not be deleted.", deleted ? "success" : "error");
        await LoadAsync(operation.Token);
    }

    private void Report(string message, string kind)
    {
        _message = message;
        _messageKind = kind;
    }
}
