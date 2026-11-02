using Ruvents.SharedKernel.Families;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningFamilyDetail(
    Guid Id,
    string Name,
    string? Description,
    FamilyRole? Role,
    IReadOnlyList<PlanningFamilyMember> Members,
    IReadOnlyList<PlanningPendingJoinRequest> PendingRequests)
{
    public bool IsOrganizer => Role is FamilyRole.Owner or FamilyRole.Organizer;
}
