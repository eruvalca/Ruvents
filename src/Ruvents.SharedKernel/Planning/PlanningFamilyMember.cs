using Ruvents.SharedKernel.Families;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningFamilyMember(
    Guid MembershipId,
    string UserId,
    string DisplayName,
    FamilyRole Role,
    DateTimeOffset JoinedAtUtc,
    bool HasPhoto);
