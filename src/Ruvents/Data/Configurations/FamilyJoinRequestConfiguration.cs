using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class FamilyJoinRequestConfiguration : IEntityTypeConfiguration<FamilyJoinRequest>
{
    public void Configure(EntityTypeBuilder<FamilyJoinRequest> builder)
    {
        builder.Property(request => request.UserId).HasMaxLength(450).IsRequired();
        builder.Property(request => request.Message).HasMaxLength(500);
        builder.Property(request => request.DecidedByUserId).HasMaxLength(450);
        builder.HasIndex(request => new { request.FamilyGroupId, request.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 0");
        builder.HasIndex(request => new { request.FamilyGroupId, request.Status });
        builder.HasOne<FamilyGroup>()
            .WithMany()
            .HasForeignKey(request => request.FamilyGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
