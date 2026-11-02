using Microsoft.AspNetCore.Components;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Admin
{
    private IReadOnlyList<PlanningAdminUser> _users = [];
    private IReadOnlyList<PlanningAdminGroup> _groups = [];
    private string _search = string.Empty;
    private string? _message;
    private string _messageKind = "information";

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _users = await Api.ListUsersAsync(null, operation.Token);
        _groups = await Api.ListGroupsAsync(operation.Token);
    }

    private async Task LoadUsersAsync()
    {
        using var operation = CreateOperation();
        _users = await Api.ListUsersAsync(_search, operation.Token);
    }

    private async Task ResetAsync(string userId)
    {
        using var operation = CreateOperation();
        var temporary = await Api.ResetPasswordAsync(userId, operation.Token);
        Report(
            temporary is null ? "The password could not be reset for that account." : $"Temporary password: {temporary}",
            temporary is null ? "error" : "success");
        await LoadUsersAsync();
    }

    private async Task SuspendAsync(string userId)
    {
        using var operation = CreateOperation();
        var suspended = await Api.SuspendUserAsync(userId, null, operation.Token);
        Report(suspended ? "Account suspended." : "That account cannot be suspended.", suspended ? "success" : "error");
        await LoadUsersAsync();
    }

    private async Task ReactivateAsync(string userId)
    {
        using var operation = CreateOperation();
        var reactivated = await Api.ReactivateUserAsync(userId, operation.Token);
        Report(reactivated ? "Account reactivated." : "That account could not be reactivated.", reactivated ? "success" : "error");
        await LoadUsersAsync();
    }

    private async Task DeleteGroupAsync(Guid familyGroupId)
    {
        using var operation = CreateOperation();
        var deleted = await Api.DeleteGroupAsync(familyGroupId, operation.Token);
        Report(deleted ? "Group removed." : "That group could not be removed.", deleted ? "success" : "error");
        _groups = await Api.ListGroupsAsync(operation.Token);
    }

    private void Report(string message, string kind)
    {
        _message = message;
        _messageKind = kind;
    }
}
