using Ruvents.SharedKernel.Notifications;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningNotification(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    string? Link,
    DateTimeOffset CreatedAtUtc,
    bool IsRead);
