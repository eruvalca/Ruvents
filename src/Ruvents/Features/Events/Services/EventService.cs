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

internal sealed class EventService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IFamilyAccess familyAccess,
    INotificationPublisher notifications) : IEventDirectory
{
    public async Task<Guid?> CreateAsync(PlanningEventInput input, string userId, CancellationToken cancellationToken)
    {
        var role = await familyAccess.GetRoleAsync(input.FamilyGroupId, userId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var calendarEvent = new FamilyEvent
        {
            FamilyGroupId = input.FamilyGroupId,
            Title = input.Title.Trim(),
            Description = Trimmed(input.Description),
            LocationName = input.LocationName.Trim(),
            LocationAddress = Trimmed(input.LocationAddress),
            MapUrl = Trimmed(input.MapLink),
            StartUtc = input.StartUtc,
            EndUtc = input.EndUtc,
            IsAllDay = input.IsAllDay,
            TimeZoneId = input.TimeZoneId,
            RsvpDeadlineUtc = input.RsvpDeadlineUtc,
            Status = EventStatus.Scheduled,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Events.Add(calendarEvent);
        // The creator organizes the event, so they are attending by default. They can
        // still change this RSVP, including which of their household members attend.
        db.EventRsvps.Add(new EventRsvp
        {
            EventId = calendarEvent.Id,
            UserId = userId,
            IsAttending = true,
            RespondedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        var members = await familyAccess.ListMemberUserIdsAsync(input.FamilyGroupId, cancellationToken);
        await notifications.PublishAsync(
            Excluding(members, userId),
            NotificationKind.EventCreated,
            "New event",
            $"{calendarEvent.Title} was added to the calendar.",
            $"/events/{calendarEvent.Id}",
            cancellationToken);
        return calendarEvent.Id;
    }

    public async Task<bool> UpdateAsync(Guid eventId, PlanningEventInput input, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await db.Events.SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (calendarEvent is null
            || !await CanManageAsync(calendarEvent.FamilyGroupId, calendarEvent.CreatedByUserId, userId, cancellationToken))
        {
            return false;
        }

        calendarEvent.Title = input.Title.Trim();
        calendarEvent.Description = Trimmed(input.Description);
        calendarEvent.LocationName = input.LocationName.Trim();
        calendarEvent.LocationAddress = Trimmed(input.LocationAddress);
        calendarEvent.MapUrl = Trimmed(input.MapLink);
        calendarEvent.StartUtc = input.StartUtc;
        calendarEvent.EndUtc = input.EndUtc;
        calendarEvent.IsAllDay = input.IsAllDay;
        calendarEvent.TimeZoneId = input.TimeZoneId;
        calendarEvent.RsvpDeadlineUtc = input.RsvpDeadlineUtc;
        calendarEvent.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var members = await familyAccess.ListMemberUserIdsAsync(calendarEvent.FamilyGroupId, cancellationToken);
        await notifications.PublishAsync(
            Excluding(members, userId),
            NotificationKind.EventUpdated,
            "Event updated",
            $"{calendarEvent.Title} changed.",
            $"/events/{calendarEvent.Id}",
            cancellationToken);
        return true;
    }

    public async Task<bool> SetStatusAsync(Guid eventId, string userId, EventStatus status, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await db.Events.SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (calendarEvent is null
            || !await CanManageAsync(calendarEvent.FamilyGroupId, calendarEvent.CreatedByUserId, userId, cancellationToken))
        {
            return false;
        }

        calendarEvent.Status = status;
        calendarEvent.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var cancelled = status == EventStatus.Cancelled;
        var members = await familyAccess.ListMemberUserIdsAsync(calendarEvent.FamilyGroupId, cancellationToken);
        await notifications.PublishAsync(
            members,
            cancelled ? NotificationKind.EventCancelled : NotificationKind.EventUpdated,
            cancelled ? "Event cancelled" : "Event restored",
            $"{calendarEvent.Title} was {(cancelled ? "cancelled" : "restored")}.",
            $"/events/{calendarEvent.Id}",
            cancellationToken);
        return true;
    }

    public async Task<PlanningEventDetail?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
            from calendarEvent in db.Events
            join familyGroup in db.FamilyGroups on calendarEvent.FamilyGroupId equals familyGroup.Id
            where calendarEvent.Id == eventId
            select new
            {
                Event = calendarEvent,
                GroupName = familyGroup.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var role = await familyAccess.GetRoleAsync(row.Event.FamilyGroupId, userId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        return new PlanningEventDetail(
            row.Event.Id,
            row.Event.FamilyGroupId,
            row.GroupName,
            row.Event.Title,
            row.Event.Description,
            row.Event.LocationName,
            row.Event.LocationAddress,
            row.Event.MapUrl,
            row.Event.StartUtc,
            row.Event.EndUtc,
            row.Event.IsAllDay,
            row.Event.TimeZoneId,
            row.Event.RsvpDeadlineUtc,
            row.Event.PotluckEnabled,
            row.Event.Status,
            row.Event.CreatedByUserId,
            CanManage(role, row.Event.CreatedByUserId, userId),
            row.Event.CoverImageBlobName is not null);
    }

    public async Task<IReadOnlyList<PlanningEventSummary>> ListForGroupAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken)
    {
        var role = await familyAccess.GetRoleAsync(familyGroupId, userId, cancellationToken);
        if (role is null)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Events
            .Where(calendarEvent => calendarEvent.FamilyGroupId == familyGroupId)
            .OrderByDescending(calendarEvent => calendarEvent.StartUtc)
            .Select(calendarEvent => new PlanningEventSummary(
                calendarEvent.Id,
                calendarEvent.FamilyGroupId,
                calendarEvent.Title,
                calendarEvent.StartUtc,
                calendarEvent.EndUtc,
                calendarEvent.IsAllDay,
                calendarEvent.TimeZoneId,
                calendarEvent.LocationName,
                calendarEvent.Status,
                calendarEvent.CoverImageBlobName != null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningEventSummary>> ListUpcomingAsync(string userId, CancellationToken cancellationToken)
    {
        var groupIds = await familyAccess.ListGroupIdsAsync(userId, cancellationToken);
        if (groupIds.Count == 0)
        {
            return [];
        }

        const int BatchSize = 25;
        const int MaxResults = 10;

        var now = timeProvider.GetUtcNow();
        // A local day can be longer than 24 hours when clocks roll back, and two-hour
        // rollbacks exist, so the candidate window is 26 hours. A timed event without an
        // end stays current until the end of its start day, and a multi-day event is
        // caught by its stored end. Finished candidates are removed before the list is
        // limited so they cannot crowd out later events.
        var threshold = now.AddHours(-26);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var results = new List<PlanningEventSummary>(MaxResults);
        var skip = 0;
        while (results.Count < MaxResults)
        {
            var batch = await db.Events
                .Where(calendarEvent => groupIds.Contains(calendarEvent.FamilyGroupId)
                    && calendarEvent.Status == EventStatus.Scheduled
                    && (calendarEvent.StartUtc >= threshold
                        || (calendarEvent.EndUtc != null && calendarEvent.EndUtc >= threshold)))
                .OrderBy(calendarEvent => calendarEvent.StartUtc)
                .ThenBy(calendarEvent => calendarEvent.Id)
                .Skip(skip)
                .Take(BatchSize)
                .Select(calendarEvent => new PlanningEventSummary(
                    calendarEvent.Id,
                    calendarEvent.FamilyGroupId,
                    calendarEvent.Title,
                    calendarEvent.StartUtc,
                    calendarEvent.EndUtc,
                    calendarEvent.IsAllDay,
                    calendarEvent.TimeZoneId,
                    calendarEvent.LocationName,
                    calendarEvent.Status,
                    calendarEvent.CoverImageBlobName != null))
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            skip += batch.Count;
            var remaining = MaxResults - results.Count;
            results.AddRange(batch
                .Where(calendarEvent => EventTiming.GetTimeState(
                    calendarEvent.StartUtc,
                    calendarEvent.EndUtc,
                    calendarEvent.IsAllDay,
                    calendarEvent.TimeZoneId,
                    now) is not EventTimeState.Past)
                .Take(remaining));

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return results;
    }

    internal async Task<bool> CanManageAsync(Guid familyGroupId, string createdByUserId, string userId, CancellationToken cancellationToken)
    {
        var role = await familyAccess.GetRoleAsync(familyGroupId, userId, cancellationToken);
        return CanManage(role, createdByUserId, userId);
    }

    private static bool CanManage(FamilyRole? role, string createdByUserId, string userId) =>
        role is FamilyRole.Owner or FamilyRole.Organizer
        || string.Equals(createdByUserId, userId, StringComparison.Ordinal);

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> Excluding(IReadOnlyList<string> userIds, string excluded) =>
        userIds.Where(id => !string.Equals(id, excluded, StringComparison.Ordinal)).ToList();
}
