using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Events.Services;

internal sealed class RsvpService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IFamilyAccess familyAccess,
    IEventAccess eventAccess) : IRsvpDirectory
{
    public async Task<RsvpSaveResult> SaveAsync(
        Guid eventId,
        string userId,
        bool isAttending,
        string? note,
        IReadOnlyCollection<Guid> attendingHouseholdMemberIds,
        IReadOnlyList<string> guests,
        CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null || await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken) is null)
        {
            return RsvpSaveResult.Forbidden;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await db.Events.SingleAsync(item => item.Id == eventId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (calendarEvent.RsvpDeadlineUtc is not null && now > calendarEvent.RsvpDeadlineUtc)
        {
            return RsvpSaveResult.Locked;
        }

        var ownHouseholdIds = await db.HouseholdMembers
            .Where(member => member.UserId == userId)
            .Select(member => member.Id)
            .ToListAsync(cancellationToken);
        if (attendingHouseholdMemberIds.Any(id => !ownHouseholdIds.Contains(id)))
        {
            return RsvpSaveResult.InvalidHouseholdMember;
        }

        var rsvp = await db.EventRsvps.SingleOrDefaultAsync(
            item => item.EventId == eventId && item.UserId == userId,
            cancellationToken);
        if (rsvp is null)
        {
            rsvp = new EventRsvp { EventId = eventId, UserId = userId };
            db.EventRsvps.Add(rsvp);
        }

        rsvp.IsAttending = isAttending;
        rsvp.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        rsvp.RespondedAtUtc = now;

        var previousAttendance = await db.EventRsvpHouseholdMembers
            .Where(item => item.EventRsvpId == rsvp.Id)
            .ToListAsync(cancellationToken);
        db.EventRsvpHouseholdMembers.RemoveRange(previousAttendance);
        foreach (var householdMemberId in attendingHouseholdMemberIds.Distinct())
        {
            db.EventRsvpHouseholdMembers.Add(new EventRsvpHouseholdMember
            {
                EventRsvpId = rsvp.Id,
                HouseholdMemberId = householdMemberId,
                IsAttending = true,
            });
        }

        var previousGuests = await db.EventRsvpGuests
            .Where(item => item.EventRsvpId == rsvp.Id)
            .ToListAsync(cancellationToken);
        db.EventRsvpGuests.RemoveRange(previousGuests);
        foreach (var guest in guests
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            db.EventRsvpGuests.Add(new EventRsvpGuest { EventRsvpId = rsvp.Id, Name = guest });
        }

        await db.SaveChangesAsync(cancellationToken);
        return RsvpSaveResult.Saved;
    }

    public async Task<PlanningRsvpState> GetOwnAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var deadline = await db.Events
            .Where(item => item.Id == eventId)
            .Select(item => item.RsvpDeadlineUtc)
            .SingleOrDefaultAsync(cancellationToken);
        var locked = deadline is not null && timeProvider.GetUtcNow() > deadline;

        var rsvp = await db.EventRsvps
            .SingleOrDefaultAsync(item => item.EventId == eventId && item.UserId == userId, cancellationToken);
        if (rsvp is null)
        {
            return new PlanningRsvpState(null, null, [], [], locked);
        }

        var household = await db.EventRsvpHouseholdMembers
            .Where(item => item.EventRsvpId == rsvp.Id && item.IsAttending)
            .Select(item => item.HouseholdMemberId)
            .ToListAsync(cancellationToken);
        var guests = await db.EventRsvpGuests
            .Where(item => item.EventRsvpId == rsvp.Id)
            .OrderBy(item => item.Name)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        return new PlanningRsvpState(rsvp.IsAttending, rsvp.Note, household, guests, locked);
    }

    public async Task<IReadOnlyList<PlanningRsvpEntry>> ListAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null || await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken) is null)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from rsvp in db.EventRsvps
            join user in db.Users on rsvp.UserId equals user.Id
            where rsvp.EventId == eventId
            orderby rsvp.RespondedAtUtc
            select new
            {
                rsvp.Id,
                rsvp.UserId,
                user.DisplayName,
                user.UserName,
                rsvp.IsAttending,
                rsvp.Note,
                rsvp.RespondedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var rsvpIds = rows.ConvertAll(row => row.Id);
        var household = await (
            from attendance in db.EventRsvpHouseholdMembers
            join member in db.HouseholdMembers on attendance.HouseholdMemberId equals member.Id
            where rsvpIds.Contains(attendance.EventRsvpId) && attendance.IsAttending
            orderby member.Name
            select new { attendance.EventRsvpId, member.Name })
            .ToListAsync(cancellationToken);
        var guests = await db.EventRsvpGuests
            .Where(guest => rsvpIds.Contains(guest.EventRsvpId))
            .OrderBy(guest => guest.Name)
            .Select(guest => new { guest.EventRsvpId, guest.Name })
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(row => new PlanningRsvpEntry(
            row.UserId,
            !string.IsNullOrWhiteSpace(row.DisplayName) ? row.DisplayName : row.UserName ?? "(unknown)",
            row.IsAttending,
            row.Note,
            household.Where(item => item.EventRsvpId == row.Id).Select(item => item.Name).ToList(),
            guests.Where(item => item.EventRsvpId == row.Id).Select(item => item.Name).ToList(),
            row.RespondedAtUtc));
    }
}
