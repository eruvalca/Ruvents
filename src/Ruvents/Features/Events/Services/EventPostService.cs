using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Notifications;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Events.Services;

internal sealed class EventPostService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IFamilyAccess familyAccess,
    IEventAccess eventAccess,
    INotificationPublisher notifications) : IEventPostDirectory
{
    public async Task<IReadOnlyList<PlanningEventPost>> ListAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null || await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken) is null)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from post in db.EventPosts
            join user in db.Users on post.AuthorUserId equals user.Id
            where post.EventId == eventId && post.DeletedAtUtc == null
            orderby post.CreatedAtUtc
            select new
            {
                post.Id,
                post.ParentPostId,
                post.Kind,
                post.AuthorUserId,
                user.DisplayName,
                user.UserName,
                post.Body,
                post.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var canModerate = await CanModerateAsync(access, userId, cancellationToken);
        return rows.ConvertAll(row => new PlanningEventPost(
            row.Id,
            row.ParentPostId,
            row.Kind,
            !string.IsNullOrWhiteSpace(row.DisplayName) ? row.DisplayName : row.UserName ?? "(unknown)",
            row.Body,
            row.CreatedAtUtc,
            canModerate || string.Equals(row.AuthorUserId, userId, StringComparison.Ordinal)));
    }

    public async Task<PostAddResult> AddAsync(
        Guid eventId,
        string userId,
        EventPostKind kind,
        string body,
        Guid? parentPostId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return PostAddResult.Empty;
        }

        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null || await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken) is null)
        {
            return PostAddResult.Forbidden;
        }

        if (kind == EventPostKind.Announcement && !await CanModerateAsync(access, userId, cancellationToken))
        {
            return PostAddResult.Forbidden;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Guid? resolvedParentId = null;
        string? parentAuthorId = null;
        if (parentPostId is not null)
        {
            var parent = await db.EventPosts.SingleOrDefaultAsync(
                post => post.Id == parentPostId && post.EventId == eventId && post.DeletedAtUtc == null,
                cancellationToken);
            // Replies attach to a top-level post only, so threads stay two levels deep.
            if (parent is null || parent.ParentPostId is not null || kind == EventPostKind.Announcement)
            {
                return PostAddResult.InvalidParent;
            }

            resolvedParentId = parent.Id;
            parentAuthorId = parent.AuthorUserId;
        }

        db.EventPosts.Add(new EventPost
        {
            EventId = eventId,
            ParentPostId = resolvedParentId,
            AuthorUserId = userId,
            Kind = kind,
            Body = body.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);

        var recipients = new List<string>();
        if (kind == EventPostKind.Announcement)
        {
            recipients.AddRange(await familyAccess.ListMemberUserIdsAsync(access.FamilyGroupId, cancellationToken));
        }
        else
        {
            recipients.Add(access.CreatedByUserId);
            if (parentAuthorId is not null)
            {
                recipients.Add(parentAuthorId);
            }
        }

        var recipientsWithoutAuthor = recipients
            .Where(id => !string.Equals(id, userId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var announcement = kind == EventPostKind.Announcement;
        await notifications.PublishAsync(
            recipientsWithoutAuthor,
            announcement ? NotificationKind.AnnouncementPosted : NotificationKind.EventUpdated,
            announcement ? "New announcement" : "New comment",
            announcement
                ? $"An announcement was posted on {access.Title}."
                : $"Someone commented on {access.Title}.",
            $"/events/{eventId}",
            cancellationToken);
        return PostAddResult.Added;
    }

    public async Task<bool> DeleteAsync(Guid postId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var post = await db.EventPosts.SingleOrDefaultAsync(
            item => item.Id == postId && item.DeletedAtUtc == null,
            cancellationToken);
        if (post is null)
        {
            return false;
        }

        var access = await eventAccess.FindAsync(post.EventId, cancellationToken);
        if (access is null)
        {
            return false;
        }

        var canModerate = await CanModerateAsync(access, userId, cancellationToken);
        if (!canModerate && !string.Equals(post.AuthorUserId, userId, StringComparison.Ordinal))
        {
            return false;
        }

        post.DeletedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> CanModerateAsync(EventAccess access, string userId, CancellationToken cancellationToken)
    {
        if (string.Equals(access.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            return true;
        }

        var role = await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken);
        return role is FamilyRole.Owner or FamilyRole.Organizer;
    }
}
