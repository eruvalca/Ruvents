using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ruvents.Data.Entities;

namespace Ruvents.Data.Configurations;

internal sealed class EventPostConfiguration : IEntityTypeConfiguration<EventPost>
{
    public void Configure(EntityTypeBuilder<EventPost> builder)
    {
        builder.Property(post => post.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.Property(post => post.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(post => new { post.EventId, post.CreatedAtUtc });
        builder.HasOne<FamilyEvent>()
            .WithMany()
            .HasForeignKey(post => post.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EventPost>()
            .WithMany()
            .HasForeignKey(post => post.ParentPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
