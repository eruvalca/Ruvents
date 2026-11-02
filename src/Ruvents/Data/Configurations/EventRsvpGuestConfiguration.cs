using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class EventRsvpGuestConfiguration : IEntityTypeConfiguration<EventRsvpGuest>
{
    public void Configure(EntityTypeBuilder<EventRsvpGuest> builder)
    {
        builder.Property(guest => guest.Name).HasMaxLength(100).IsRequired();
        builder.HasOne<EventRsvp>()
            .WithMany()
            .HasForeignKey(guest => guest.EventRsvpId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
