using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data;

namespace Ruvents.Data.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(100);
        builder.Property(user => user.SuspendedReason).HasMaxLength(500);
        builder.Property(user => user.ProfilePhotoBlobName).HasMaxLength(300);
        builder.Property(user => user.ProfilePhotoContentType).HasMaxLength(100);
    }
}
