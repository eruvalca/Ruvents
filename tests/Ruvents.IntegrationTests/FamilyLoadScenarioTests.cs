using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ruvents.Data;
using Ruvents.Features.Administration.Contracts;
using Ruvents.Features.Administration.Services;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Events.Services;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Families.Services;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Notifications.Services;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.Features.Potluck.Services;
using Ruvents.IntegrationTests.Support;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Planning;
using Shouldly;
using Xunit;

namespace Ruvents.IntegrationTests;

/// <summary>
/// Realistic family load: 15 siblings, each with four household members, planning
/// events, potluck and discussion together. These tests exercise the application
/// services against real PostgreSQL instead of a mock.
/// </summary>
[Collection(PostgresAppFixture.CollectionName)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class FamilyLoadScenarioTests(PostgresAppFixture fixture)
{
    private const int SiblingCount = 15;
    private const int ChildrenPerSibling = 4;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FifteenSiblingsAndTheirChildrenCanAllRespondToTheSameEvent()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var (ownerId, groupId, siblingIds) = await services.CreateBigFamilyAsync("RSVP");

        var attendeeIds = new List<string>(siblingIds);
        var households = new Dictionary<string, List<GuidedHousehold>>(StringComparer.Ordinal);
        foreach (var siblingId in attendeeIds)
        {
            var members = await services.AddHouseholdAsync(siblingId, "Kid", ChildrenPerSibling);
            households[siblingId] = members;
        }

        var eventId = await services.CreateEventAsync(groupId, ownerId, "Summer barbecue", fixture.Clock.Now.AddDays(10));

        foreach (var siblingId in attendeeIds)
        {
            var children = households[siblingId].Take(2).Select(member => member.Id).ToList();
            var saved = await services.Rsvps.SaveAsync(
                eventId,
                siblingId,
                isAttending: true,
                note: "See you there",
                children,
                [$"Guest of {siblingId[..6]}"],
                Token);
            saved.ShouldBe(RsvpSaveResult.Saved);
        }

        var responses = await services.Rsvps.ListAsync(eventId, ownerId, Token);
        // 15 siblings plus the creator, who is attending by default.
        responses.Count.ShouldBe(SiblingCount + 1);
        responses.ShouldAllBe(entry => entry.IsAttending);
        responses.Sum(entry => entry.HouseholdAttendees.Count).ShouldBe(SiblingCount * 2);
        responses.Sum(entry => entry.Guests.Count).ShouldBe(SiblingCount);

        // Re-saving replaces the previous household selection instead of duplicating it.
        var repeatSibling = attendeeIds[0];
        var oneChild = households[repeatSibling].Take(1).Select(member => member.Id).ToList();
        (await services.Rsvps.SaveAsync(eventId, repeatSibling, true, "Updated", oneChild, [], Token))
            .ShouldBe(RsvpSaveResult.Saved);

        var state = await services.Rsvps.GetOwnAsync(eventId, repeatSibling, Token);
        state.Note.ShouldBe("Updated");
        state.AttendingHouseholdMemberIds.Count.ShouldBe(1);
        state.Guests.ShouldBeEmpty();
        (await services.Rsvps.ListAsync(eventId, ownerId, Token)).Count.ShouldBe(SiblingCount + 1);
    }

    [Fact]
    public async Task PotluckNeverOverSubscribesWhenEveryoneClaimsAtOnce()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var (ownerId, groupId, siblingIds) = await services.CreateBigFamilyAsync("Potluck");

        var eventId = await services.CreateEventAsync(groupId, ownerId, "Potluck supper", fixture.Clock.Now.AddDays(5));
        (await services.Potluck.EnableAsync(eventId, ownerId, true, Token)).ShouldBe(PotluckResult.Ok);
        (await services.Potluck.AddSlotAsync(eventId, ownerId, "Main dish", "Anything hearty", 10, Token))
            .ShouldBe(PotluckResult.Ok);

        var board = (await services.Potluck.GetAsync(eventId, ownerId, Token)).ShouldNotBeNull();
        var slotId = board.Slots.Single().Id;

        // Fifteen people race for ten places.
        var results = await Task.WhenAll(siblingIds.Select(siblingId =>
            services.Potluck.ClaimAsync(slotId, siblingId, 1, "Dish", Token)));

        results.Count(result => result == PotluckResult.Ok).ShouldBe(10);
        results.ShouldNotContain(PotluckResult.Conflict);

        var slot = (await services.Potluck.GetAsync(eventId, ownerId, Token)).ShouldNotBeNull().Slots.Single();
        slot.QuantityClaimed.ShouldBe(10);
        slot.Claims.Count.ShouldBe(10);

        // Removing a claim frees a place for someone else.
        var firstClaim = slot.Claims[0];
        (await services.Potluck.UnclaimAsync(slotId, firstClaim.UserId, Token)).ShouldBe(PotluckResult.Ok);
        var waitlisted = siblingIds.First(id => slot.Claims.All(claim => !string.Equals(claim.UserId, id, StringComparison.Ordinal)));
        (await services.Potluck.ClaimAsync(slotId, waitlisted, 1, null, Token)).ShouldBe(PotluckResult.Ok);

        // The organizer can rename and resize a slot, and clear another member's claim.
        (await services.Potluck.UpdateSlotAsync(slotId, ownerId, "Main dish (updated)", "Bring two", 12, Token))
            .ShouldBe(PotluckResult.Ok);
        var currentClaims = (await services.Potluck.GetAsync(eventId, ownerId, Token)).ShouldNotBeNull().Slots.Single().Claims;
        var ownedByMember = currentClaims.First(claim => !string.Equals(claim.UserId, ownerId, StringComparison.Ordinal));
        (await services.Potluck.RemoveClaimAsync(slotId, ownedByMember.UserId, siblingIds[1], Token))
            .ShouldBe(PotluckResult.Forbidden);
        (await services.Potluck.RemoveClaimAsync(slotId, ownedByMember.UserId, ownerId, Token))
            .ShouldBe(PotluckResult.Ok);
        (await services.Potluck.AssignAsync(slotId, siblingIds[2], ownerId, 3, "Dessert", Token))
            .ShouldBe(PotluckResult.Ok);

        // A member can add their own item, claimed by them from the start.
        (await services.Potluck.AddCustomItemAsync(eventId, siblingIds[0], "Grandma's pie", 1, "Apple", Token))
            .ShouldBe(PotluckResult.Ok);
        var custom = (await services.Potluck.GetAsync(eventId, ownerId, Token))
            .ShouldNotBeNull()
            .Slots.Single(item => string.Equals(item.Name, "Grandma's pie", StringComparison.Ordinal));
        custom.IsCustom.ShouldBeTrue();
        custom.Claims.ShouldHaveSingleItem().UserId.ShouldBe(siblingIds[0]);
    }

    [Fact]
    public async Task DiscussionThreadsAndNotificationsStayConsistentForALargeFamily()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var (ownerId, groupId, siblingIds) = await services.CreateBigFamilyAsync("Discussion");
        var eventId = await services.CreateEventAsync(groupId, ownerId, "Reunion planning", fixture.Clock.Now.AddDays(30));

        (await services.Posts.AddAsync(eventId, ownerId, EventPostKind.Announcement, "Bring photos", null, Token))
            .ShouldBe(PostAddResult.Added);

        foreach (var siblingId in siblingIds)
        {
            (await services.Posts.AddAsync(eventId, siblingId, EventPostKind.Comment, "Count me in", null, Token))
                .ShouldBe(PostAddResult.Added);
        }

        var posts = await services.Posts.ListAsync(eventId, ownerId, Token);
        posts.Count.ShouldBe(SiblingCount + 1);
        var announcement = posts.Single(post => post.Kind == EventPostKind.Announcement);

        // Replies attach to a top-level post only; a reply to a reply is refused.
        var reply = await services.Posts.AddAsync(eventId, siblingIds[0], EventPostKind.Comment, "Me too", announcement.Id, Token);
        reply.ShouldBe(PostAddResult.Added);
        var replyId = (await services.Posts.ListAsync(eventId, ownerId, Token)).Last(post => post.ParentPostId == announcement.Id).Id;
        (await services.Posts.AddAsync(eventId, siblingIds[1], EventPostKind.Comment, "Nested", replyId, Token))
            .ShouldBe(PostAddResult.InvalidParent);

        // Members cannot post announcements; the organizer can moderate any post.
        (await services.Posts.AddAsync(eventId, siblingIds[2], EventPostKind.Announcement, "Hijack", null, Token))
            .ShouldBe(PostAddResult.Forbidden);
        var memberPostId = (await services.Posts.ListAsync(eventId, siblingIds[3], Token))
            .First(post => post.CanDelete && post.Kind == EventPostKind.Comment).Id;
        (await services.Posts.DeleteAsync(memberPostId, ownerId, Token)).ShouldBeTrue();

        // Everyone was notified and can clear the badge.
        foreach (var siblingId in siblingIds.Take(3))
        {
            (await services.Notifications.UnreadCountAsync(siblingId, Token)).ShouldBeGreaterThan(0);
            await services.Notifications.MarkAllReadAsync(siblingId, Token);
            (await services.Notifications.UnreadCountAsync(siblingId, Token)).ShouldBe(0);
        }
    }

    [Fact]
    public async Task DeadlinesPermissionsAndCancellationBehaveForALargeFamily()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var (ownerId, groupId, siblingIds) = await services.CreateBigFamilyAsync("Rules");
        var outsider = await services.CreateUserAsync("Outsider");

        var deadline = fixture.Clock.Now.AddDays(2);
        var eventId = await services.CreateEventAsync(groupId, ownerId, "RSVP deadline", fixture.Clock.Now.AddDays(9), deadline);

        // Past the deadline nobody can respond, including the organizer.
        fixture.Clock.Advance(TimeSpan.FromDays(3));
        (await services.Rsvps.SaveAsync(eventId, siblingIds[0], true, null, [], [], Token)).ShouldBe(RsvpSaveResult.Locked);

        // The creator can reopen by clearing the deadline.
        var detail = (await services.Events.GetAsync(eventId, ownerId, Token)).ShouldNotBeNull();
        var reopened = await services.Events.UpdateAsync(
            eventId,
            new PlanningEventInput(groupId, detail.Title, detail.Description, detail.LocationName, detail.LocationAddress, detail.MapLink, detail.StartUtc, detail.EndUtc, detail.IsAllDay, detail.TimeZoneId, null),
            ownerId,
            Token);
        reopened.ShouldBeTrue();
        (await services.Rsvps.SaveAsync(eventId, siblingIds[0], true, null, [], [], Token)).ShouldBe(RsvpSaveResult.Saved);

        // Non-members cannot see or respond.
        (await services.Events.GetAsync(eventId, outsider.Id, Token)).ShouldBeNull();
        (await services.Rsvps.SaveAsync(eventId, outsider.Id, true, null, [], [], Token)).ShouldBe(RsvpSaveResult.Forbidden);

        // Household members belong to one account only.
        var foreignHousehold = await services.AddHouseholdAsync(outsider.Id, "Kid", 1);
        (await services.Rsvps.SaveAsync(eventId, siblingIds[1], true, null, [foreignHousehold[0].Id], [], Token))
            .ShouldBe(RsvpSaveResult.InvalidHouseholdMember);

        // Only the creator or an organizer can cancel; both can.
        var memberId = siblingIds[2];
        (await services.Events.SetStatusAsync(eventId, memberId, EventStatus.Cancelled, Token)).ShouldBeFalse();
        (await services.Events.SetStatusAsync(eventId, ownerId, EventStatus.Cancelled, Token)).ShouldBeTrue();
        (await services.Events.GetAsync(eventId, ownerId, Token)).ShouldNotBeNull().Status.ShouldBe(EventStatus.Cancelled);
    }

    [Fact]
    public async Task GroupSearchDoesNotMatchEverythingWhenTheNameContainsWildcards()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var owner = await services.CreateUserAsync("Searcher");
        _ = await services.Families.CreateAsync(owner.Id, "100% Family_", null, Token);
        _ = await services.Families.CreateAsync(owner.Id, "Ordinary Family", null, Token);

        var matches = await services.Families.SearchAsync("%", owner.Id, Token);
        matches.ShouldContain(item => item.Name == "100% Family_");
        matches.ShouldNotContain(item => item.Name == "Ordinary Family");
    }

    [Fact]
    public async Task ManyEventsStayResponsiveAndOrdered()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var owner = await services.CreateUserAsync("Busy organizer");
        var groupId = await services.Families.CreateAsync(owner.Id, "Busy Family", null, Token);

        var stopwatch = Stopwatch.StartNew();
        var start = fixture.Clock.Now.Date.AddDays(-30);
        for (var index = 0; index < 120; index++)
        {
            _ = await services.Events.CreateAsync(
                new PlanningEventInput(
                    groupId,
                    $"Event {index}",
                    null,
                    "Community hall",
                    null,
                    null,
                    new DateTimeOffset(start.AddDays(index), TimeSpan.Zero),
                    new DateTimeOffset(start.AddDays(index).AddHours(2), TimeSpan.Zero),
                    false,
                    "UTC",
                    null),
                owner.Id,
                Token);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));

        (await services.Events.ListForGroupAsync(groupId, owner.Id, Token)).Count.ShouldBe(120);

        var upcoming = await services.Events.ListUpcomingAsync(owner.Id, Token);
        upcoming.Count.ShouldBe(10);
        upcoming.ShouldBe(upcoming.OrderBy(item => item.StartUtc));
        upcoming.ShouldAllBe(item => item.Status == EventStatus.Scheduled);

        // Cancelling removes an event from the upcoming list without affecting the group list.
        var cancelled = await services.Events.SetStatusAsync(upcoming[0].Id, owner.Id, EventStatus.Cancelled, Token);
        cancelled.ShouldBeTrue();
        var afterCancel = await services.Events.ListUpcomingAsync(owner.Id, Token);
        afterCancel.ShouldNotContain(item => item.Id == upcoming[0].Id);
        (await services.Events.ListForGroupAsync(groupId, owner.Id, Token)).Count.ShouldBe(120);
    }

    [Fact]
    public async Task FinishedEventsDoNotCrowdOutLaterUpcomingEvents()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var owner = await services.CreateUserAsync("Late planner");
        var groupId = await services.Families.CreateAsync(owner.Id, "Late Family", null, Token);

        // These started inside the one-day lookback but have already ended, so they must
        // not consume the limited result slots ahead of the event that is still ahead.
        for (var index = 0; index < 10; index++)
        {
            var start = fixture.Clock.Now.AddHours(-12).AddMinutes(index);
            _ = await services.Events.CreateAsync(
                new PlanningEventInput(
                    groupId,
                    $"Finished {index}",
                    null,
                    "Community hall",
                    null,
                    null,
                    start,
                    start.AddMinutes(30),
                    false,
                    "UTC",
                    null),
                owner.Id,
                Token);
        }

        var upcomingStart = fixture.Clock.Now.AddDays(2);
        var upcomingId = await services.Events.CreateAsync(
            new PlanningEventInput(
                groupId,
                "Still ahead",
                null,
                "Community hall",
                null,
                null,
                upcomingStart,
                upcomingStart.AddHours(2),
                false,
                "UTC",
                null),
            owner.Id,
            Token);

        var upcoming = await services.Events.ListUpcomingAsync(owner.Id, Token);

        upcoming.Count.ShouldBe(1);
        upcoming.ShouldContain(item => item.Id == upcomingId);
    }

    [Fact]
    public async Task OngoingMultiDayEventsStayInTheDashboardCandidates()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var owner = await services.CreateUserAsync("Multi-day host");
        var groupId = await services.Families.CreateAsync(owner.Id, "Multi-day Family", null, Token);

        // Started before the one-day lookback but still running, so a start-only cutoff
        // would drop it while the shared state calculation reports it as in progress.
        var ongoingStart = fixture.Clock.Now.AddDays(-3);
        _ = await services.Events.CreateAsync(
            new PlanningEventInput(
                groupId,
                "Still running",
                null,
                "Cabin",
                null,
                null,
                ongoingStart,
                fixture.Clock.Now.AddHours(4),
                false,
                "UTC",
                null),
            owner.Id,
            Token);

        var upcomingStart = fixture.Clock.Now.AddDays(2);
        _ = await services.Events.CreateAsync(
            new PlanningEventInput(
                groupId,
                "Later",
                null,
                "Community hall",
                null,
                null,
                upcomingStart,
                upcomingStart.AddHours(2),
                false,
                "UTC",
                null),
            owner.Id,
            Token);

        var upcoming = await services.Events.ListUpcomingAsync(owner.Id, Token);

        upcoming.Count.ShouldBe(2);
        upcoming[0].Title.ShouldBe("Still running");
        upcoming[1].Title.ShouldBe("Later");
    }

    [Fact]
    public async Task UpcomingPagingSpansATieGroupAcrossBatchesAsync()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var owner = await services.CreateUserAsync("Tie organizer");
        var groupId = await services.Families.CreateAsync(owner.Id, "Tie Family", null, Token);

        // Thirty events share one start instant, so the first 25-row batch ends inside the
        // tie. They have no end and started 25 hours ago, so they are finished but still
        // inside the candidate window; the exact state filter must discard them and the
        // loop must continue to the live events.
        var tieStart = fixture.Clock.Now.AddHours(-25);
        for (var index = 0; index < 30; index++)
        {
            _ = await services.Events.CreateAsync(
                new PlanningEventInput(
                    groupId,
                    $"Tie {index}",
                    null,
                    "Community hall",
                    null,
                    null,
                    tieStart,
                    null,
                    false,
                    "UTC",
                    null),
                owner.Id,
                Token);
        }

        var liveStart = fixture.Clock.Now.AddHours(-1);
        for (var index = 0; index < 5; index++)
        {
            _ = await services.Events.CreateAsync(
                new PlanningEventInput(
                    groupId,
                    $"Live {index}",
                    null,
                    "Community hall",
                    null,
                    null,
                    liveStart,
                    liveStart.AddHours(2),
                    false,
                    "UTC",
                    null),
                owner.Id,
                Token);
        }

        var upcoming = await services.Events.ListUpcomingAsync(owner.Id, Token);

        upcoming.Count.ShouldBe(5);
        upcoming.Select(item => item.Id).Distinct().Count().ShouldBe(5);
        upcoming.ShouldAllBe(item => item.Title.StartsWith("Live", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdministrationModeratesAndRemovesALargeFamilyWithoutLeavingOrphans()
    {
        await using var scope = fixture.CreateScope();
        var services = new ScenarioServices(scope.ServiceProvider);
        var (ownerId, groupId, siblingIds) = await services.CreateBigFamilyAsync("Admin", householdPerMember: 1);
        var eventId = await services.CreateEventAsync(groupId, ownerId, "To be removed", fixture.Clock.Now.AddDays(4));
        (await services.Rsvps.SaveAsync(eventId, siblingIds[0], true, null, [], [], Token)).ShouldBe(RsvpSaveResult.Saved);
        _ = await services.Posts.AddAsync(eventId, ownerId, EventPostKind.Announcement, "Hello", null, Token);

        var users = await services.Administration.ListUsersAsync("Sibling", Token);
        users.Count.ShouldBeGreaterThanOrEqualTo(SiblingCount);

        var target = siblingIds[0];
        (await services.Administration.SuspendAsync(target, "Testing suspension", Token)).ShouldBeTrue();
        var suspended = (await services.Administration.ListUsersAsync("Sibling", Token))
            .First(user => string.Equals(user.Id, target, StringComparison.Ordinal));
        suspended.IsSuspended.ShouldBeTrue();
        (await services.Administration.ReactivateAsync(target, Token)).ShouldBeTrue();

        var groups = await services.Administration.ListGroupsAsync(Token);
        var listed = groups.First(item => item.Id == groupId);
        listed.MemberCount.ShouldBe(SiblingCount + 1);
        listed.EventCount.ShouldBe(1);

        (await services.Administration.DeleteGroupAsync(groupId, Token)).ShouldBeTrue();
        (await services.Administration.ListGroupsAsync(Token)).ShouldNotContain(item => item.Id == groupId);

        await using var verification = fixture.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await database.Events.CountAsync(item => item.FamilyGroupId == groupId, Token)).ShouldBe(0);
        (await database.FamilyMemberships.CountAsync(item => item.FamilyGroupId == groupId, Token)).ShouldBe(0);
        (await database.EventRsvps.CountAsync(item => item.EventId == eventId, Token)).ShouldBe(0);
        (await database.EventPosts.CountAsync(item => item.EventId == eventId, Token)).ShouldBe(0);
        (await database.HouseholdMembers.CountAsync(item => siblingIds.Contains(item.UserId), Token))
            .ShouldBe(SiblingCount);
    }

    private sealed class ScenarioServices
    {
        internal ScenarioServices(IServiceProvider services)
        {
            Services = services;
            Users = services.GetRequiredService<UserManager<ApplicationUser>>();
            Families = services.GetRequiredService<IFamilyGroups>();
            Joins = services.GetRequiredService<IJoinRequests>();
            Household = services.GetRequiredService<IHouseholdDirectory>();
            Events = services.GetRequiredService<EventService>();
            Rsvps = services.GetRequiredService<IRsvpDirectory>();
            Posts = services.GetRequiredService<IEventPostDirectory>();
            Potluck = services.GetRequiredService<IPotluckBoard>();
            Notifications = services.GetRequiredService<INotificationQueries>();
            Administration = services.GetRequiredService<AdministrationService>();
        }

        internal IServiceProvider Services { get; }

        internal UserManager<ApplicationUser> Users { get; }

        internal IFamilyGroups Families { get; }

        internal IJoinRequests Joins { get; }

        internal IHouseholdDirectory Household { get; }

        internal EventService Events { get; }

        internal IRsvpDirectory Rsvps { get; }

        internal IEventPostDirectory Posts { get; }

        internal IPotluckBoard Potluck { get; }

        internal INotificationQueries Notifications { get; }

        internal AdministrationService Administration { get; }

        internal async Task<ApplicationUser> CreateUserAsync(string displayName)
        {
            var unique = Guid.NewGuid().ToString("N");
            var user = new ApplicationUser
            {
                UserName = $"{unique}@example.test",
                Email = $"{unique}@example.test",
                DisplayName = displayName,
            };
            (await Users.CreateAsync(user)).Succeeded.ShouldBeTrue();
            return user;
        }

        internal async Task<(string OwnerId, Guid GroupId, List<string> SiblingIds)> CreateBigFamilyAsync(
            string label,
            int householdPerMember = 0)
        {
            var owner = await CreateUserAsync("Pat");
            var siblingIds = new List<string>(SiblingCount);
            for (var index = 1; index <= SiblingCount; index++)
            {
                siblingIds.Add((await CreateUserAsync($"Sibling {index}")).Id);
            }

            var groupId = await Families.CreateAsync(owner.Id, $"{label} Family {Guid.NewGuid():N}", null, Token);
            foreach (var siblingId in siblingIds)
            {
                (await Joins.RequestAsync(groupId, siblingId, "Please add me", Token)).ShouldBe(JoinRequestResult.Requested);
            }

            var detail = await Families.GetAsync(groupId, owner.Id, Token);
            detail.ShouldNotBeNull();
            detail.PendingRequests.Count.ShouldBe(SiblingCount);
            foreach (var pending in detail.PendingRequests)
            {
                (await Joins.DecideAsync(pending.Id, owner.Id, true, Token)).ShouldBe(JoinDecisionResult.Decided);
            }

            (await Families.GetAsync(groupId, owner.Id, Token)).ShouldNotBeNull().Members.Count.ShouldBe(SiblingCount + 1);

            if (householdPerMember > 0)
            {
                foreach (var siblingId in siblingIds)
                {
                    _ = await AddHouseholdAsync(siblingId, "Household", householdPerMember);
                }
            }

            return (owner.Id, groupId, siblingIds);
        }

        internal async Task<List<GuidedHousehold>> AddHouseholdAsync(string userId, string prefix, int count)
        {
            for (var index = 1; index <= count; index++)
            {
                await Household.AddAsync(userId, $"{prefix} {index}", Token);
            }

            var members = await Household.ListAsync(userId, Token);
            return members.Select(member => new GuidedHousehold(member.Id, member.Name)).ToList();
        }

        internal async Task<Guid> CreateEventAsync(Guid groupId, string creatorId, string title, DateTimeOffset start, DateTimeOffset? deadline = null)
        {
            var eventId = await Events.CreateAsync(
                new PlanningEventInput(
                    groupId,
                    title,
                    "Details for the family",
                    "Backyard",
                    null,
                    null,
                    start,
                    start.AddHours(3),
                    false,
                    "UTC",
                    deadline),
                creatorId,
                Token);
            eventId.ShouldNotBeNull();
            return eventId.Value;
        }
    }

    private sealed record GuidedHousehold(Guid Id, string Name);
}
