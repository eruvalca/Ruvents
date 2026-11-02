using Ruvents.SharedKernel.Events;

namespace Ruvents.Data.Entities;

internal sealed class FamilyEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FamilyGroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? LocationAddress { get; set; }

    public string? MapUrl { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public bool IsAllDay { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public DateTimeOffset? RsvpDeadlineUtc { get; set; }

    public string? CoverImageBlobName { get; set; }

    public string? CoverImageContentType { get; set; }

    public bool PotluckEnabled { get; set; }

    public EventStatus Status { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
