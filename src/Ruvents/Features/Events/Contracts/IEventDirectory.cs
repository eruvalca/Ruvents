using Ruvents.SharedKernel.Events;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Events.Contracts;

internal interface IEventDirectory
{
    Task<Guid?> CreateAsync(PlanningEventInput input, string userId, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(Guid eventId, PlanningEventInput input, string userId, CancellationToken cancellationToken);

    Task<bool> SetStatusAsync(Guid eventId, string userId, EventStatus status, CancellationToken cancellationToken);

    Task<PlanningEventDetail?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningEventSummary>> ListForGroupAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningEventSummary>> ListUpcomingAsync(string userId, CancellationToken cancellationToken);
}
