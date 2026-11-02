using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.UI.Features.Planning.Pages;

public sealed partial class Notifications
{
    private IReadOnlyList<PlanningNotification> _items = [];
    private string? _message;
    private string _messageKind = "information";

    protected override async Task OnInitializedAsync()
    {
        using var operation = CreateOperation();
        _items = await Api.ListNotificationsAsync(50, operation.Token);
    }

    private async Task MarkReadAsync(Guid notificationId)
    {
        using var operation = CreateOperation();
        await Api.MarkNotificationReadAsync(notificationId, operation.Token);
        _items = await Api.ListNotificationsAsync(50, operation.Token);
        _message = "Notification marked as read.";
        _messageKind = "success";
    }

    private async Task MarkAllAsync()
    {
        using var operation = CreateOperation();
        await Api.MarkAllNotificationsReadAsync(operation.Token);
        _items = await Api.ListNotificationsAsync(50, operation.Token);
        _message = "All notifications marked as read.";
        _messageKind = "success";
    }
}
