namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningAdminUser(
    string Id,
    string Email,
    string DisplayName,
    bool IsAdmin,
    bool IsSuspended,
    string? SuspendedReason,
    bool HasPhoto,
    DateTimeOffset? SuspendedAtUtc);
