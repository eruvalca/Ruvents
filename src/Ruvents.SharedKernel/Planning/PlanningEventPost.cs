using Ruvents.SharedKernel.Events;

namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningEventPost(
    Guid Id,
    Guid? ParentPostId,
    EventPostKind Kind,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAtUtc,
    bool CanDelete);
