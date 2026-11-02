using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Groups
{
    private IReadOnlyList<PlanningFamilyListItem> _groups = [];
    private IReadOnlyList<PlanningFamilySearchItem> _results = [];
    private readonly CreateModel _createModel = new();
    private string _search = string.Empty;
    private bool _searched;
    private string? _message;
    private string _messageKind = "information";

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _groups = await Api.ListFamiliesAsync(operation.Token);
    }

    private async Task CreateAsync()
    {
        using var operation = CreateOperation();
        var name = _createModel.Name.Trim();
        if (name.Length is < 2 or > 120)
        {
            Report("Enter a group name between 2 and 120 characters.", "error");
            return;
        }

        var groupId = await Api.CreateFamilyAsync(name, _createModel.Description, operation.Token);
        Navigation.NavigateTo($"groups/{groupId}");
    }

    private async Task SearchAsync() =>
        await LoadLatestAsync(
            token => Api.SearchFamiliesAsync(_search, token),
            results =>
            {
                _results = results;
                _searched = true;
            });

    private async Task JoinAsync(Guid familyGroupId)
    {
        using var operation = CreateOperation();
        var result = await Api.RequestJoinAsync(familyGroupId, null, operation.Token);
        Report(
            result switch
            {
                JoinRequestResult.Requested => "Your request to join was sent.",
                JoinRequestResult.AlreadyMember => "You are already a member of that group.",
                JoinRequestResult.AlreadyRequested => "You already have a pending request for that group.",
                _ => "That group could not be found.",
            },
            result == JoinRequestResult.Requested ? "success" : "error");
        // Refresh through the same guard so a slower, older search cannot replace it.
        await LoadLatestAsync(
            token => Api.SearchFamiliesAsync(_search, token),
            results => _results = results);
    }

    private void Report(string message, string kind)
    {
        _message = message;
        _messageKind = kind;
    }

    private sealed class CreateModel
    {
        [Required]
        [StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }
    }
}
