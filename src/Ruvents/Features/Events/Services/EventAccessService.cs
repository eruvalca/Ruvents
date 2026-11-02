using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Features.Events.Contracts;

namespace Ruvents.Features.Events.Services;

internal sealed class EventAccessService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IEventAccess
{
    public async Task<EventAccess?> FindAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Events
            .Where(calendarEvent => calendarEvent.Id == eventId)
            .Select(calendarEvent => new EventAccess(
                calendarEvent.Id,
                calendarEvent.FamilyGroupId,
                calendarEvent.Title,
                calendarEvent.CreatedByUserId,
                calendarEvent.PotluckEnabled,
                calendarEvent.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
