namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningFamilySearchItem(Guid Id, string Name, string? Description, int MemberCount, bool IsMember, bool HasPendingRequest);
