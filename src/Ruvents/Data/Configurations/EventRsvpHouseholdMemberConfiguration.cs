using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class EventRsvpHouseholdMemberConfiguration : IEntityTypeConfiguration<EventRsvpHouseholdMember>
{
    public void Configure(EntityTypeBuilder<EventRsvpHouseholdMember> builder)
    {
        builder.HasIndex(attendance => new { attendance.EventRsvpId, attendance.HouseholdMemberId }).IsUnique();
        builder.HasOne<EventRsvp>()
            .WithMany()
            .HasForeignKey(attendance => attendance.EventRsvpId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(attendance => attendance.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
