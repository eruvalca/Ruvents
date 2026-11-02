namespace Ruvents.Data.Entities;

internal sealed class EventRsvpHouseholdMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventRsvpId { get; set; }

    public Guid HouseholdMemberId { get; set; }

    public bool IsAttending { get; set; }
}
