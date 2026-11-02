using Ruvents.SharedKernel.Families;

namespace Ruvents.Data.Entities;

internal sealed class FamilyJoinRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FamilyGroupId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public JoinRequestStatus Status { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public string? DecidedByUserId { get; set; }
}
