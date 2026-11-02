using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class FamilyGroupConfiguration : IEntityTypeConfiguration<FamilyGroup>
{
    public void Configure(EntityTypeBuilder<FamilyGroup> builder)
    {
        builder.Property(group => group.Name).HasMaxLength(120).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(2000);
        builder.Property(group => group.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(group => group.Name);
    }
}
