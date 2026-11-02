using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Notifications;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Services;

internal sealed class JoinRequestService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IFamilyAccess familyAccess,
    INotificationPublisher notifications) : IJoinRequests
{
    public async Task<JoinRequestResult> RequestAsync(Guid familyGroupId, string userId, string? message, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.FamilyGroups.SingleOrDefaultAsync(item => item.Id == familyGroupId, cancellationToken);
        if (group is null)
        {
            return JoinRequestResult.GroupNotFound;
        }

        if (await db.FamilyMemberships.AnyAsync(
                membership => membership.FamilyGroupId == familyGroupId && membership.UserId == userId,
                cancellationToken))
        {
            return JoinRequestResult.AlreadyMember;
        }

        if (await db.FamilyJoinRequests.AnyAsync(
                request => request.FamilyGroupId == familyGroupId
                    && request.UserId == userId
                    && request.Status == JoinRequestStatus.Pending,
                cancellationToken))
        {
            return JoinRequestResult.AlreadyRequested;
        }

        db.FamilyJoinRequests.Add(new FamilyJoinRequest
        {
            FamilyGroupId = familyGroupId,
            UserId = userId,
            Status = JoinRequestStatus.Pending,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            RequestedAtUtc = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);

        var organizers = await familyAccess.ListOrganizerUserIdsAsync(familyGroupId, cancellationToken);
        await notifications.PublishAsync(
            organizers,
            NotificationKind.JoinRequestReceived,
            "New join request",
            $"Someone asked to join {group.Name}.",
            $"/groups/{group.Id}",
            cancellationToken);
        return JoinRequestResult.Requested;
    }

    public async Task<JoinDecisionResult> DecideAsync(Guid requestId, string actingUserId, bool approve, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var request = await db.FamilyJoinRequests.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null)
        {
            return JoinDecisionResult.NotFound;
        }

        var role = await familyAccess.GetRoleAsync(request.FamilyGroupId, actingUserId, cancellationToken);
        if (role is not (FamilyRole.Owner or FamilyRole.Organizer))
        {
            return JoinDecisionResult.Forbidden;
        }

        if (request.Status != JoinRequestStatus.Pending)
        {
            return JoinDecisionResult.AlreadyDecided;
        }

        var now = timeProvider.GetUtcNow();
        request.Status = approve ? JoinRequestStatus.Approved : JoinRequestStatus.Rejected;
        request.DecidedAtUtc = now;
        request.DecidedByUserId = actingUserId;

        if (approve && !await db.FamilyMemberships.AnyAsync(
                membership => membership.FamilyGroupId == request.FamilyGroupId && membership.UserId == request.UserId,
                cancellationToken))
        {
            db.FamilyMemberships.Add(new FamilyMembership
            {
                FamilyGroupId = request.FamilyGroupId,
                UserId = request.UserId,
                Role = FamilyRole.Member,
                JoinedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var groupName = await db.FamilyGroups
            .Where(group => group.Id == request.FamilyGroupId)
            .Select(group => group.Name)
            .SingleAsync(cancellationToken);
        await notifications.PublishAsync(
            [request.UserId],
            NotificationKind.JoinRequestDecided,
            approve ? "Join request approved" : "Join request declined",
            approve ? $"You are now a member of {groupName}." : $"Your request to join {groupName} was declined.",
            approve ? $"/groups/{request.FamilyGroupId}" : null,
            cancellationToken);
        return JoinDecisionResult.Decided;
    }
}
