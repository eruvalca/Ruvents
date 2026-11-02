using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(notification => notification.UserId).HasMaxLength(450).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Body).HasMaxLength(1000).IsRequired();
        builder.Property(notification => notification.LinkUrl).HasMaxLength(500);
        builder.HasIndex(notification => new { notification.UserId, notification.ReadAtUtc });
    }
}
