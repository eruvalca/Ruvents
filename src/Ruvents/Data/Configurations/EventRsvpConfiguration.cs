using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class EventRsvpConfiguration : IEntityTypeConfiguration<EventRsvp>
{
    public void Configure(EntityTypeBuilder<EventRsvp> builder)
    {
        builder.Property(rsvp => rsvp.UserId).HasMaxLength(450).IsRequired();
        builder.Property(rsvp => rsvp.Note).HasMaxLength(1000);
        builder.HasIndex(rsvp => new { rsvp.EventId, rsvp.UserId }).IsUnique();
        builder.HasOne<FamilyEvent>()
            .WithMany()
            .HasForeignKey(rsvp => rsvp.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
