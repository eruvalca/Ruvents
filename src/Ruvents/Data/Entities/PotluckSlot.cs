namespace Ruvents.Data.Entities;

internal sealed class PotluckSlot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int QuantityNeeded { get; set; }

    public bool IsCustom { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>
    /// Optimistic concurrency token incremented by every claim change so two members
    /// cannot over-claim a slot at the same time.
    /// </summary>
    public int ClaimVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
