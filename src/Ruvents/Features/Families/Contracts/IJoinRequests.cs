using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Contracts;

internal interface IJoinRequests
{
    Task<JoinRequestResult> RequestAsync(Guid familyGroupId, string userId, string? message, CancellationToken cancellationToken);

    Task<JoinDecisionResult> DecideAsync(Guid requestId, string actingUserId, bool approve, CancellationToken cancellationToken);
}
