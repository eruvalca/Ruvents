using Ruvents.SharedKernel.Notifications;

namespace Ruvents.Data.Entities;

internal sealed class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string UserId { get; set; } = string.Empty;

    public NotificationKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? LinkUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ReadAtUtc { get; set; }
}
