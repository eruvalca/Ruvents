namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningPendingJoinRequest(Guid Id, string UserId, string DisplayName, string? Message, DateTimeOffset RequestedAtUtc);
