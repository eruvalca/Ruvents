namespace Ruvents.Data.Entities;

internal sealed class PotluckClaim
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PotluckSlotId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? DishNote { get; set; }

    /// <summary>
    /// Set when an organizer assigned this claim on another member's behalf.
    /// </summary>
    public string? AssignedByUserId { get; set; }

    public DateTimeOffset ClaimedAtUtc { get; set; }
}
