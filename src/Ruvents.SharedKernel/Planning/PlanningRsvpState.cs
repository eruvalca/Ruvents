namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningRsvpState(
    bool? IsAttending,
    string? Note,
    IReadOnlyList<Guid> AttendingHouseholdMemberIds,
    IReadOnlyList<string> Guests,
    bool IsLocked);
