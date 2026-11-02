using Ruvents.SharedKernel.Events;

namespace Ruvents.Features.Events.Contracts;

internal sealed record EventAccess(
    Guid EventId,
    Guid FamilyGroupId,
    string Title,
    string CreatedByUserId,
    bool PotluckEnabled,
    EventStatus Status);
