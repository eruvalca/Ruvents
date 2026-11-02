namespace Ruvents.Data.Entities;

internal sealed class EventRsvp
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public bool IsAttending { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset RespondedAtUtc { get; set; }
}
