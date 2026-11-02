using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Dashboard
{
    private IReadOnlyList<PlanningEventSummary> _upcoming = [];
    private IReadOnlyList<PlanningFamilyListItem> _groups = [];
    private int _unread;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _upcoming = await Api.ListUpcomingEventsAsync(operation.Token);
        _groups = await Api.ListFamiliesAsync(operation.Token);
        _unread = await Api.UnreadCountAsync(operation.Token);
        _loading = false;
    }
}
