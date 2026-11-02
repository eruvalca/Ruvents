using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.Property(member => member.UserId).HasMaxLength(450).IsRequired();
        builder.Property(member => member.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(member => member.UserId);
    }
}
