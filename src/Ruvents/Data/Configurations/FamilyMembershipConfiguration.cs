using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class FamilyMembershipConfiguration : IEntityTypeConfiguration<FamilyMembership>
{
    public void Configure(EntityTypeBuilder<FamilyMembership> builder)
    {
        builder.Property(membership => membership.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(membership => new { membership.FamilyGroupId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => membership.UserId);
        builder.HasOne<FamilyGroup>()
            .WithMany()
            .HasForeignKey(membership => membership.FamilyGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
