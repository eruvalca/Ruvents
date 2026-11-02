using Ruvents.SharedKernel.Families;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningFamilyListItem(Guid Id, string Name, string? Description, FamilyRole Role, int MemberCount);
