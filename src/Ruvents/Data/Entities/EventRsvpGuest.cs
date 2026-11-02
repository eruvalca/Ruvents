namespace Ruvents.Data.Entities;

internal sealed class EventRsvpGuest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventRsvpId { get; set; }

    public string Name { get; set; } = string.Empty;
}
