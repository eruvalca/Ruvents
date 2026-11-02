using Microsoft.AspNetCore.Components;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class GroupDetail
{
    private PlanningFamilyDetail? _group;
    private IReadOnlyList<PlanningEventSummary> _events = [];
    private string? _message;
    private string _messageKind = "information";

    [Parameter]
    public Guid GroupId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        await LoadAsync(operation.Token);
    }

    private static string Initial(string displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? "?" : displayName[..1].ToUpperInvariant();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _group = await Api.GetFamilyAsync(GroupId, cancellationToken);
        _events = _group is null ? [] : await Api.ListGroupEventsAsync(GroupId, cancellationToken);
    }

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
