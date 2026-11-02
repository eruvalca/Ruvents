using Ruvents.SharedKernel.Families;

namespace Ruvents.Data.Entities;

internal sealed class FamilyMembership
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FamilyGroupId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public FamilyRole Role { get; set; }

    public DateTimeOffset JoinedAtUtc { get; set; }
}
