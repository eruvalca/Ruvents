using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Planning;

namespace Ruvents.UI.Planning;

/// <summary>
/// Data access for the planning pages. The server host implements this with the
/// application services; the WebAssembly host implements it over the JSON API, so
/// the same components run under either renderer.
/// </summary>
public interface IPlanningApi
{
    Task<IReadOnlyList<PlanningFamilyListItem>> ListFamiliesAsync(CancellationToken cancellationToken);

    Task<PlanningFamilyDetail?> GetFamilyAsync(Guid familyGroupId, CancellationToken cancellationToken);

    Task<Guid> CreateFamilyAsync(string name, string? description, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningFamilySearchItem>> SearchFamiliesAsync(string query, CancellationToken cancellationToken);

    Task<JoinRequestResult> RequestJoinAsync(Guid familyGroupId, string? message, CancellationToken cancellationToken);

    Task<JoinDecisionResult> DecideJoinRequestAsync(Guid requestId, bool approve, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningHouseholdMember>> ListHouseholdAsync(CancellationToken cancellationToken);

    Task AddHouseholdMemberAsync(string name, CancellationToken cancellationToken);

    Task<bool> RenameHouseholdMemberAsync(Guid householdMemberId, string name, CancellationToken cancellationToken);

    Task<bool> RemoveHouseholdMemberAsync(Guid householdMemberId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningEventSummary>> ListUpcomingEventsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningEventSummary>> ListGroupEventsAsync(Guid familyGroupId, CancellationToken cancellationToken);

    Task<PlanningEventDetail?> GetEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Guid?> CreateEventAsync(PlanningEventInput input, CancellationToken cancellationToken);

    Task<bool> UpdateEventAsync(Guid eventId, PlanningEventInput input, CancellationToken cancellationToken);

    Task<bool> SetEventStatusAsync(Guid eventId, bool cancelled, CancellationToken cancellationToken);

    Task<PlanningRsvpState> GetRsvpAsync(Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningRsvpEntry>> ListRsvpsAsync(Guid eventId, CancellationToken cancellationToken);

    Task<RsvpSaveResult> SaveRsvpAsync(
        Guid eventId,
        bool attending,
        string? note,
        IReadOnlyCollection<Guid> householdMemberIds,
        IReadOnlyList<string> guests,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningEventPost>> ListPostsAsync(Guid eventId, CancellationToken cancellationToken);

    Task<PostAddResult> AddPostAsync(Guid eventId, bool announcement, string body, Guid? parentPostId, CancellationToken cancellationToken);

    Task<bool> DeletePostAsync(Guid postId, CancellationToken cancellationToken);

    Task<PlanningPotluckBoard?> GetPotluckAsync(Guid eventId, CancellationToken cancellationToken);

    Task<PotluckResult> SetPotluckEnabledAsync(Guid eventId, bool enabled, CancellationToken cancellationToken);

    Task<PotluckResult> AddPotluckSlotAsync(Guid eventId, string name, string? description, int quantity, CancellationToken cancellationToken);

    Task<PotluckResult> AddCustomPotluckItemAsync(Guid eventId, string name, int quantity, string? dishNote, CancellationToken cancellationToken);

    Task<PotluckResult> RemovePotluckSlotAsync(Guid slotId, CancellationToken cancellationToken);

    Task<PotluckResult> ClaimPotluckSlotAsync(Guid slotId, int quantity, string? dishNote, CancellationToken cancellationToken);

    Task<PotluckResult> UnclaimPotluckSlotAsync(Guid slotId, CancellationToken cancellationToken);

    Task<PotluckResult> UpdatePotluckSlotAsync(Guid slotId, string name, string? description, int quantity, CancellationToken cancellationToken);

    Task<PotluckResult> RemovePotluckClaimAsync(Guid slotId, string claimUserId, CancellationToken cancellationToken);

    Task<PotluckResult> AssignPotluckSlotAsync(Guid slotId, string userId, int quantity, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningNotification>> ListNotificationsAsync(int take, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(CancellationToken cancellationToken);

    Task MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningAdminUser>> ListUsersAsync(string? search, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningAdminGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken);

    Task<bool> SuspendUserAsync(string userId, string? reason, CancellationToken cancellationToken);

    Task<bool> ReactivateUserAsync(string userId, CancellationToken cancellationToken);

    Task<bool> DeleteGroupAsync(Guid familyGroupId, CancellationToken cancellationToken);

    Task<bool> UploadEventCoverAsync(Guid eventId, Stream content, string contentType, CancellationToken cancellationToken);
}
