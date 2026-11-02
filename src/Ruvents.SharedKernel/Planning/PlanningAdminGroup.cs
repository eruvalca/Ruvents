namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningAdminGroup(Guid Id, string Name, int MemberCount, int EventCount, DateTimeOffset CreatedAtUtc);
