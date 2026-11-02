namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningRsvpEntry(
    string UserId,
    string DisplayName,
    bool IsAttending,
    string? Note,
    IReadOnlyList<string> HouseholdAttendees,
    IReadOnlyList<string> Guests,
    DateTimeOffset RespondedAtUtc);
