using Ruvents.SharedKernel.Events;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningEventDetail(
    Guid Id,
    Guid FamilyGroupId,
    string GroupName,
    string Title,
    string? Description,
    string LocationName,
    string? LocationAddress,
    string? MapLink,
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    bool IsAllDay,
    string TimeZoneId,
    DateTimeOffset? RsvpDeadlineUtc,
    bool PotluckEnabled,
    EventStatus Status,
    string CreatedByUserId,
    bool CanManage,
    bool HasCoverImage);
