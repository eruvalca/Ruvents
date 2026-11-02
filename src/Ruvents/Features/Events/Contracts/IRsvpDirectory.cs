using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Events.Contracts;

internal interface IRsvpDirectory
{
    Task<RsvpSaveResult> SaveAsync(
        Guid eventId,
        string userId,
        bool isAttending,
        string? note,
        IReadOnlyCollection<Guid> attendingHouseholdMemberIds,
        IReadOnlyList<string> guests,
        CancellationToken cancellationToken);

    Task<PlanningRsvpState> GetOwnAsync(Guid eventId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningRsvpEntry>> ListAsync(Guid eventId, string userId, CancellationToken cancellationToken);
}
