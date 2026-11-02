namespace Ruvents.Features.Events.Contracts;

internal interface IEventAccess
{
    Task<EventAccess?> FindAsync(Guid eventId, CancellationToken cancellationToken);
}
