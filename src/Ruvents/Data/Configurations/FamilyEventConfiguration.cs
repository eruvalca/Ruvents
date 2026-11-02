using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class FamilyEventConfiguration : IEntityTypeConfiguration<FamilyEvent>
{
    public void Configure(EntityTypeBuilder<FamilyEvent> builder)
    {
        builder.Property(calendarEvent => calendarEvent.Title).HasMaxLength(200).IsRequired();
        builder.Property(calendarEvent => calendarEvent.Description).HasMaxLength(4000);
        builder.Property(calendarEvent => calendarEvent.LocationName).HasMaxLength(200).IsRequired();
        builder.Property(calendarEvent => calendarEvent.LocationAddress).HasMaxLength(500);
        builder.Property(calendarEvent => calendarEvent.MapUrl).HasMaxLength(2000);
        builder.Property(calendarEvent => calendarEvent.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(calendarEvent => calendarEvent.CoverImageBlobName).HasMaxLength(300);
        builder.Property(calendarEvent => calendarEvent.CoverImageContentType).HasMaxLength(100);
        builder.Property(calendarEvent => calendarEvent.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(calendarEvent => new { calendarEvent.FamilyGroupId, calendarEvent.StartUtc });
        builder.HasOne<FamilyGroup>()
            .WithMany()
            .HasForeignKey(calendarEvent => calendarEvent.FamilyGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
