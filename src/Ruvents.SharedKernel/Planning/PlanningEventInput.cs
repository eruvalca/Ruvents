namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningEventInput(
    Guid FamilyGroupId,
    string Title,
    string? Description,
    string LocationName,
    string? LocationAddress,
    string? MapLink,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    bool IsAllDay,
    string TimeZoneId,
    DateTimeOffset? RsvpDeadlineUtc);
