using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class PotluckClaimConfiguration : IEntityTypeConfiguration<PotluckClaim>
{
    public void Configure(EntityTypeBuilder<PotluckClaim> builder)
    {
        builder.Property(claim => claim.UserId).HasMaxLength(450).IsRequired();
        builder.Property(claim => claim.DishNote).HasMaxLength(300);
        builder.Property(claim => claim.AssignedByUserId).HasMaxLength(450);
        builder.HasIndex(claim => new { claim.PotluckSlotId, claim.UserId }).IsUnique();
        builder.HasOne<PotluckSlot>()
            .WithMany()
            .HasForeignKey(claim => claim.PotluckSlotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
