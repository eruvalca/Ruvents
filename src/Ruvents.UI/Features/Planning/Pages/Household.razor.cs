using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Household
{
    private IReadOnlyList<PlanningHouseholdMember> _members = [];
    private string _newName = string.Empty;
    private string? _message;
    private string _messageKind = "information";

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _members = await Api.ListHouseholdAsync(operation.Token);
    }

    private async Task AddAsync()
    {
        using var operation = CreateOperation();
        var name = _newName.Trim();
        if (name.Length is < 1 or > 100)
        {
            Report("Enter a name up to 100 characters.", "error");
            return;
        }

        await Api.AddHouseholdMemberAsync(name, operation.Token);
        _newName = string.Empty;
        _members = await Api.ListHouseholdAsync(operation.Token);
        Report("Household member added.", "success");
    }

    private async Task RenameAsync(Guid householdMemberId, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        using var operation = CreateOperation();
        var renamed = await Api.RenameHouseholdMemberAsync(householdMemberId, name.Trim(), operation.Token);
        Report(renamed ? "Household member renamed." : "That member could not be renamed.", renamed ? "success" : "error");
        _members = await Api.ListHouseholdAsync(operation.Token);
    }

    private async Task RemoveAsync(Guid householdMemberId)
    {
        using var operation = CreateOperation();
        var removed = await Api.RemoveHouseholdMemberAsync(householdMemberId, operation.Token);
        Report(removed ? "Household member removed." : "That member could not be removed.", removed ? "success" : "error");
        _members = await Api.ListHouseholdAsync(operation.Token);
    }

    private void Report(string message, string kind)
    {
        _message = message;
        _messageKind = kind;
    }
}
