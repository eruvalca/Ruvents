using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Potluck.Contracts;

internal interface IPotluckBoard
{
    Task<PlanningPotluckBoard?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken);

    Task<PotluckResult> EnableAsync(Guid eventId, string userId, bool enabled, CancellationToken cancellationToken);

    Task<PotluckResult> AddSlotAsync(
        Guid eventId,
        string userId,
        string name,
        string? description,
        int quantityNeeded,
        CancellationToken cancellationToken);

    Task<PotluckResult> AddCustomItemAsync(
        Guid eventId,
        string userId,
        string name,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken);

    Task<PotluckResult> UpdateSlotAsync(
        Guid slotId,
        string userId,
        string name,
        string? description,
        int quantityNeeded,
        CancellationToken cancellationToken);

    Task<PotluckResult> RemoveSlotAsync(Guid slotId, string userId, CancellationToken cancellationToken);

    Task<PotluckResult> ClaimAsync(
        Guid slotId,
        string userId,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken);

    Task<PotluckResult> UnclaimAsync(Guid slotId, string userId, CancellationToken cancellationToken);

    Task<PotluckResult> RemoveClaimAsync(Guid slotId, string claimUserId, string actingUserId, CancellationToken cancellationToken);

    Task<PotluckResult> AssignAsync(
        Guid slotId,
        string assigneeUserId,
        string actingUserId,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken);
}
