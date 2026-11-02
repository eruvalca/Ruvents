using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ruvents.Data.Entities;

namespace Ruvents.Data;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<FamilyGroup> FamilyGroups => Set<FamilyGroup>();

    public DbSet<FamilyMembership> FamilyMemberships => Set<FamilyMembership>();

    public DbSet<FamilyJoinRequest> FamilyJoinRequests => Set<FamilyJoinRequest>();

    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    public DbSet<FamilyEvent> Events => Set<FamilyEvent>();

    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();

    public DbSet<EventRsvpHouseholdMember> EventRsvpHouseholdMembers => Set<EventRsvpHouseholdMember>();

    public DbSet<EventRsvpGuest> EventRsvpGuests => Set<EventRsvpGuest>();

    public DbSet<PotluckSlot> PotluckSlots => Set<PotluckSlot>();

    public DbSet<PotluckClaim> PotluckClaims => Set<PotluckClaim>();

    public DbSet<EventPost> EventPosts => Set<EventPost>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
