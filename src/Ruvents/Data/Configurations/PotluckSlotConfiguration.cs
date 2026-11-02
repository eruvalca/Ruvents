using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class PotluckSlotConfiguration : IEntityTypeConfiguration<PotluckSlot>
{
    public void Configure(EntityTypeBuilder<PotluckSlot> builder)
    {
        builder.Property(slot => slot.Name).HasMaxLength(120).IsRequired();
        builder.Property(slot => slot.Description).HasMaxLength(500);
        builder.Property(slot => slot.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(slot => slot.ClaimVersion).IsConcurrencyToken();
        builder.HasIndex(slot => new { slot.EventId, slot.SortOrder });
        builder.HasOne<FamilyEvent>()
            .WithMany()
            .HasForeignKey(slot => slot.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
