using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Account.Services;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Events.Services;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.IntegrationTests.Support;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Notifications;
using Ruvents.SharedKernel.Planning;
using Shouldly;
using Xunit;

namespace Ruvents.IntegrationTests;

/// <summary>
/// Account deletion must clear the planning data the account owns. Identity has no
/// foreign keys into those tables, so without this the rows outlive the account.
/// </summary>
[Collection(PostgresAppFixture.CollectionName)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountDeletionTests(PostgresAppFixture fixture)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeletingAnAccountClearsItsPlanningDataAndFreesPotluckCapacity()
    {
        await using var scope = fixture.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var families = services.GetRequiredService<IFamilyGroups>();
        var events = services.GetRequiredService<EventService>();
        var rsvps = services.GetRequiredService<IRsvpDirectory>();
        var potluck = services.GetRequiredService<IPotluckBoard>();
        var notifications = services.GetRequiredService<INotificationPublisher>();
        var deletion = services.GetRequiredService<AccountDeletionService>();
        var database = services.GetRequiredService<ApplicationDbContext>();

        var owner = await CreateUserAsync(users, "Organizer");
        var member = await CreateUserAsync(users, "Sibling");
        var groupId = await families.CreateAsync(owner.Id, $"Delete {Guid.NewGuid():N}", null, Token);

        var household = new HouseholdMember
        {
            UserId = member.Id,
            Name = "Child",
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
        };
        database.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyGroupId = groupId,
            UserId = member.Id,
            JoinedAtUtc = DateTimeOffset.UnixEpoch,
        });
        database.HouseholdMembers.Add(household);
        await database.SaveChangesAsync(Token);

        var eventId = (await events.CreateAsync(
            new PlanningEventInput(groupId, "Delete test", null, "Home", null, null,
                DateTimeOffset.UnixEpoch.AddDays(1), null, false, "UTC", null),
            owner.Id,
            Token)).ShouldNotBeNull();

        (await rsvps.SaveAsync(eventId, member.Id, true, "Bringing bread", [household.Id], [], Token))
            .ShouldBe(RsvpSaveResult.Saved);
        (await potluck.EnableAsync(eventId, owner.Id, true, Token)).ShouldBe(PotluckResult.Ok);
        (await potluck.AddSlotAsync(eventId, owner.Id, "Main dish", null, 1, Token)).ShouldBe(PotluckResult.Ok);
        var slotId = (await potluck.GetAsync(eventId, owner.Id, Token)).ShouldNotBeNull().Slots.Single().Id;
        (await potluck.ClaimAsync(slotId, member.Id, 1, "Lasagna", Token)).ShouldBe(PotluckResult.Ok);
        await notifications.PublishAsync([member.Id], NotificationKind.EventCreated, "Hello", "Body", null, Token);

        // The claim currently fills the only place, and the household child is recorded.
        (await potluck.GetAsync(eventId, owner.Id, Token)).ShouldNotBeNull()
            .Slots.Single().Claims.Count.ShouldBe(1);
        (await database.EventRsvpHouseholdMembers.CountAsync(row => row.HouseholdMemberId == household.Id, Token))
            .ShouldBe(1);

        (await deletion.DeleteAccountAsync(member, Token)).ShouldBeTrue();
        database.ChangeTracker.Clear();

        (await database.Users.CountAsync(item => item.Id == member.Id, Token)).ShouldBe(0);
        (await database.FamilyMemberships.CountAsync(item => item.UserId == member.Id, Token)).ShouldBe(0);
        (await database.HouseholdMembers.CountAsync(item => item.UserId == member.Id, Token)).ShouldBe(0);
        (await database.EventRsvps.CountAsync(item => item.UserId == member.Id, Token)).ShouldBe(0);
        (await database.EventRsvpHouseholdMembers.CountAsync(row => row.HouseholdMemberId == household.Id, Token)).ShouldBe(0);
        (await database.PotluckClaims.CountAsync(item => item.UserId == member.Id, Token)).ShouldBe(0);
        (await database.Notifications.CountAsync(item => item.UserId == member.Id, Token)).ShouldBe(0);

        // Group content survives: the family keeps the event the deleted account responded to.
        (await database.Events.CountAsync(item => item.Id == eventId, Token)).ShouldBe(1);
        (await database.FamilyGroups.CountAsync(item => item.Id == groupId, Token)).ShouldBe(1);

        // The freed place can be claimed again instead of the slot reporting Full forever.
        (await potluck.GetAsync(eventId, owner.Id, Token)).ShouldNotBeNull()
            .Slots.Single().Claims.ShouldBeEmpty();
        (await potluck.ClaimAsync(slotId, owner.Id, 1, "Now mine", Token)).ShouldBe(PotluckResult.Ok);
    }

    [Fact]
    public async Task DeletingTheLastOrganizerTransfersOwnershipToASurvivingMember()
    {
        await using var scope = fixture.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var families = services.GetRequiredService<IFamilyGroups>();
        var deletion = services.GetRequiredService<AccountDeletionService>();
        var database = services.GetRequiredService<ApplicationDbContext>();

        var owner = await CreateUserAsync(users, "Only organizer");
        var successor = await CreateUserAsync(users, "Longest member");
        var groupId = await families.CreateAsync(owner.Id, $"Transfer {Guid.NewGuid():N}", null, Token);
        database.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyGroupId = groupId,
            UserId = successor.Id,
            JoinedAtUtc = DateTimeOffset.UnixEpoch,
        });
        await database.SaveChangesAsync(Token);

        (await deletion.DeleteAccountAsync(owner, Token)).ShouldBeTrue();
        database.ChangeTracker.Clear();

        // The surviving family keeps an organizer, so it can still approve join requests.
        var promoted = await database.FamilyMemberships
            .SingleAsync(item => item.FamilyGroupId == groupId && item.UserId == successor.Id, Token);
        promoted.Role.ShouldBe(FamilyRole.Owner);
        (await database.FamilyMemberships.CountAsync(item => item.UserId == owner.Id, Token)).ShouldBe(0);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string displayName)
    {
        var unique = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser
        {
            UserName = $"{unique}@example.test",
            Email = $"{unique}@example.test",
            DisplayName = displayName,
        };
        (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
        return user;
    }
}
