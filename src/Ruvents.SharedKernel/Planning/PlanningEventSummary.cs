using Ruvents.SharedKernel.Events;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningEventSummary(
    Guid Id,
    Guid FamilyGroupId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    bool IsAllDay,
    string TimeZoneId,
    string LocationName,
    EventStatus Status,
    bool HasCoverImage);
