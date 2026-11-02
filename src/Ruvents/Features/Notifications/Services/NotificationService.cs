using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.SharedKernel.Notifications;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Notifications.Services;

internal sealed class NotificationService(IDbContextFactory<ApplicationDbContext> dbContextFactory, TimeProvider timeProvider)
    : INotificationPublisher, INotificationQueries
{
    public async Task PublishAsync(
        IReadOnlyCollection<string> userIds,
        NotificationKind kind,
        string title,
        string body,
        string? linkUrl,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Kind = kind,
                Title = title,
                Body = body,
                LinkUrl = linkUrl,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications.CountAsync(notification => notification.UserId == userId && notification.ReadAtUtc == null, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningNotification>> ListAsync(string userId, int take, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(take)
            .Select(notification => new PlanningNotification(
                notification.Id,
                notification.Kind,
                notification.Title,
                notification.Body,
                notification.LinkUrl,
                notification.CreatedAtUtc,
                notification.ReadAtUtc != null))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await db.Notifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);
        if (notification is null || notification.ReadAtUtc is not null)
        {
            return;
        }

        notification.ReadAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await db.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAtUtc, now), cancellationToken);
    }
}
