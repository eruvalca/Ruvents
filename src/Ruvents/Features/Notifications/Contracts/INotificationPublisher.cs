using Ruvents.SharedKernel.Notifications;

namespace Ruvents.Features.Notifications.Contracts;

internal interface INotificationPublisher
{
    Task PublishAsync(
        IReadOnlyCollection<string> userIds,
        NotificationKind kind,
        string title,
        string body,
        string? linkUrl,
        CancellationToken cancellationToken);
}
