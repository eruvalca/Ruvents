namespace Ruvents.Data.Entities;

internal sealed class HouseholdMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
