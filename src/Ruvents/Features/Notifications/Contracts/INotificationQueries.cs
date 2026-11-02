using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Notifications.Contracts;

internal interface INotificationQueries
{
    Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningNotification>> ListAsync(string userId, int take, CancellationToken cancellationToken);

    Task MarkReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(string userId, CancellationToken cancellationToken);
}
