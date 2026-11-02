using Ruvents.SharedKernel.Events;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Events.Contracts;

internal interface IEventPostDirectory
{
    Task<IReadOnlyList<PlanningEventPost>> ListAsync(Guid eventId, string userId, CancellationToken cancellationToken);

    Task<PostAddResult> AddAsync(
        Guid eventId,
        string userId,
        EventPostKind kind,
        string body,
        Guid? parentPostId,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid postId, string userId, CancellationToken cancellationToken);
}
