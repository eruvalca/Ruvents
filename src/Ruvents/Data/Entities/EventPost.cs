using Ruvents.SharedKernel.Events;

namespace Ruvents.Data.Entities;

internal sealed class EventPost
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }

    /// <summary>
    /// Null for a top-level post. Replies point at a top-level post, so the thread is at most two levels deep.
    /// </summary>
    public Guid? ParentPostId { get; set; }

    public string AuthorUserId { get; set; } = string.Empty;

    public EventPostKind Kind { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
