using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Ruvents.Features.Administration.Contracts;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.Features.PlanningApi;

/// <summary>
/// Server-side implementation used during static rendering and server interactivity.
/// It calls the application services directly; the WebAssembly host uses the JSON API.
/// </summary>
internal sealed class ServerPlanningApi(
    IFamilyGroups families,
    IJoinRequests joinRequests,
    IHouseholdDirectory household,
    IEventDirectory events,
    IRsvpDirectory rsvps,
    IEventPostDirectory posts,
    IEventCovers covers,
    IPotluckBoard potluck,
    INotificationQueries notifications,
    IAdministration administration,
    AuthenticationStateProvider authenticationState)
    : IPlanningApi
{
    public async Task<IReadOnlyList<PlanningFamilyListItem>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        await families.ListMineAsync(await CurrentUserIdAsync(), cancellationToken);

    public async Task<PlanningFamilyDetail?> GetFamilyAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        await families.GetAsync(familyGroupId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<Guid> CreateFamilyAsync(string name, string? description, CancellationToken cancellationToken) =>
        await families.CreateAsync(await CurrentUserIdAsync(), name, description, cancellationToken);

    public async Task<IReadOnlyList<PlanningFamilySearchItem>> SearchFamiliesAsync(string query, CancellationToken cancellationToken) =>
        await families.SearchAsync(query, await CurrentUserIdAsync(), cancellationToken);

    public async Task<JoinRequestResult> RequestJoinAsync(Guid familyGroupId, string? message, CancellationToken cancellationToken) =>
        await joinRequests.RequestAsync(familyGroupId, await CurrentUserIdAsync(), message, cancellationToken);

    public async Task<JoinDecisionResult> DecideJoinRequestAsync(Guid requestId, bool approve, CancellationToken cancellationToken) =>
        await joinRequests.DecideAsync(requestId, await CurrentUserIdAsync(), approve, cancellationToken);

    public async Task<IReadOnlyList<PlanningHouseholdMember>> ListHouseholdAsync(CancellationToken cancellationToken) =>
        await household.ListAsync(await CurrentUserIdAsync(), cancellationToken);

    public async Task AddHouseholdMemberAsync(string name, CancellationToken cancellationToken) =>
        await household.AddAsync(await CurrentUserIdAsync(), name, cancellationToken);

    public async Task<bool> RenameHouseholdMemberAsync(Guid householdMemberId, string name, CancellationToken cancellationToken) =>
        await household.RenameAsync(householdMemberId, await CurrentUserIdAsync(), name, cancellationToken);

    public async Task<bool> RemoveHouseholdMemberAsync(Guid householdMemberId, CancellationToken cancellationToken) =>
        await household.RemoveAsync(householdMemberId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<IReadOnlyList<PlanningEventSummary>> ListUpcomingEventsAsync(CancellationToken cancellationToken) =>
        await events.ListUpcomingAsync(await CurrentUserIdAsync(), cancellationToken);

    public async Task<IReadOnlyList<PlanningEventSummary>> ListGroupEventsAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        await events.ListForGroupAsync(familyGroupId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PlanningEventDetail?> GetEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        await events.GetAsync(eventId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<Guid?> CreateEventAsync(PlanningEventInput input, CancellationToken cancellationToken) =>
        await events.CreateAsync(input, await CurrentUserIdAsync(), cancellationToken);

    public async Task<bool> UpdateEventAsync(Guid eventId, PlanningEventInput input, CancellationToken cancellationToken) =>
        await events.UpdateAsync(eventId, input, await CurrentUserIdAsync(), cancellationToken);

    public async Task<bool> SetEventStatusAsync(Guid eventId, bool cancelled, CancellationToken cancellationToken) =>
        await events.SetStatusAsync(
            eventId,
            await CurrentUserIdAsync(),
            cancelled ? SharedKernel.Events.EventStatus.Cancelled : SharedKernel.Events.EventStatus.Scheduled,
            cancellationToken);

    public async Task<PlanningRsvpState> GetRsvpAsync(Guid eventId, CancellationToken cancellationToken) =>
        await rsvps.GetOwnAsync(eventId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<IReadOnlyList<PlanningRsvpEntry>> ListRsvpsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await rsvps.ListAsync(eventId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<RsvpSaveResult> SaveRsvpAsync(
        Guid eventId,
        bool attending,
        string? note,
        IReadOnlyCollection<Guid> householdMemberIds,
        IReadOnlyList<string> guests,
        CancellationToken cancellationToken) =>
        await rsvps.SaveAsync(eventId, await CurrentUserIdAsync(), attending, note, householdMemberIds, guests, cancellationToken);

    public async Task<IReadOnlyList<PlanningEventPost>> ListPostsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await posts.ListAsync(eventId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PostAddResult> AddPostAsync(
        Guid eventId,
        bool announcement,
        string body,
        Guid? parentPostId,
        CancellationToken cancellationToken) =>
        await posts.AddAsync(
            eventId,
            await CurrentUserIdAsync(),
            announcement ? SharedKernel.Events.EventPostKind.Announcement : SharedKernel.Events.EventPostKind.Comment,
            body,
            parentPostId,
            cancellationToken);

    public async Task<bool> DeletePostAsync(Guid postId, CancellationToken cancellationToken) =>
        await posts.DeleteAsync(postId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PlanningPotluckBoard?> GetPotluckAsync(Guid eventId, CancellationToken cancellationToken) =>
        await potluck.GetAsync(eventId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PotluckResult> SetPotluckEnabledAsync(Guid eventId, bool enabled, CancellationToken cancellationToken) =>
        await potluck.EnableAsync(eventId, await CurrentUserIdAsync(), enabled, cancellationToken);

    public async Task<PotluckResult> AddPotluckSlotAsync(
        Guid eventId,
        string name,
        string? description,
        int quantity,
        CancellationToken cancellationToken) =>
        await potluck.AddSlotAsync(eventId, await CurrentUserIdAsync(), name, description, quantity, cancellationToken);

    public async Task<PotluckResult> AddCustomPotluckItemAsync(
        Guid eventId,
        string name,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken) =>
        await potluck.AddCustomItemAsync(eventId, await CurrentUserIdAsync(), name, quantity, dishNote, cancellationToken);

    public async Task<PotluckResult> RemovePotluckSlotAsync(Guid slotId, CancellationToken cancellationToken) =>
        await potluck.RemoveSlotAsync(slotId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PotluckResult> ClaimPotluckSlotAsync(Guid slotId, int quantity, string? dishNote, CancellationToken cancellationToken) =>
        await potluck.ClaimAsync(slotId, await CurrentUserIdAsync(), quantity, dishNote, cancellationToken);

    public async Task<PotluckResult> UnclaimPotluckSlotAsync(Guid slotId, CancellationToken cancellationToken) =>
        await potluck.UnclaimAsync(slotId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PotluckResult> UpdatePotluckSlotAsync(
        Guid slotId,
        string name,
        string? description,
        int quantity,
        CancellationToken cancellationToken) =>
        await potluck.UpdateSlotAsync(slotId, await CurrentUserIdAsync(), name, description, quantity, cancellationToken);

    public async Task<PotluckResult> RemovePotluckClaimAsync(Guid slotId, string claimUserId, CancellationToken cancellationToken) =>
        await potluck.RemoveClaimAsync(slotId, claimUserId, await CurrentUserIdAsync(), cancellationToken);

    public async Task<PotluckResult> AssignPotluckSlotAsync(Guid slotId, string userId, int quantity, CancellationToken cancellationToken) =>
        await potluck.AssignAsync(slotId, userId, await CurrentUserIdAsync(), quantity, null, cancellationToken);

    public async Task<IReadOnlyList<PlanningNotification>> ListNotificationsAsync(int take, CancellationToken cancellationToken) =>
        await notifications.ListAsync(await CurrentUserIdAsync(), take, cancellationToken);

    public async Task<int> UnreadCountAsync(CancellationToken cancellationToken) =>
        await notifications.UnreadCountAsync(await CurrentUserIdAsync(), cancellationToken);

    public async Task MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        await notifications.MarkReadAsync(notificationId, await CurrentUserIdAsync(), cancellationToken);

    public async Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken) =>
        await notifications.MarkAllReadAsync(await CurrentUserIdAsync(), cancellationToken);

    public async Task<IReadOnlyList<PlanningAdminUser>> ListUsersAsync(string? search, CancellationToken cancellationToken) =>
        await administration.ListUsersAsync(search, cancellationToken);

    public async Task<IReadOnlyList<PlanningAdminGroup>> ListGroupsAsync(CancellationToken cancellationToken) =>
        await administration.ListGroupsAsync(cancellationToken);

    public async Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken) =>
        await administration.ResetPasswordAsync(userId, cancellationToken);

    public async Task<bool> SuspendUserAsync(string userId, string? reason, CancellationToken cancellationToken) =>
        await administration.SuspendAsync(userId, reason, cancellationToken);

    public async Task<bool> ReactivateUserAsync(string userId, CancellationToken cancellationToken) =>
        await administration.ReactivateAsync(userId, cancellationToken);

    public async Task<bool> DeleteGroupAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        await administration.DeleteGroupAsync(familyGroupId, cancellationToken);

    public async Task<bool> UploadEventCoverAsync(Guid eventId, Stream content, string contentType, CancellationToken cancellationToken) =>
        await covers.SaveAsync(eventId, await CurrentUserIdAsync(), content, contentType, cancellationToken);

    private async Task<string> CurrentUserIdAsync()
    {
        var state = await authenticationState.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}
