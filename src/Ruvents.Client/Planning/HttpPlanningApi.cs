using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;

namespace Ruvents.Client.Planning;

/// <summary>WebAssembly data access over the planning JSON API.</summary>
[SuppressMessage("Design", "CA2234:Pass system uri objects instead of strings",
    Justification = "Every request uses a relative route resolved against HttpClient.BaseAddress, so a relative string is intentional.")]
internal sealed class HttpPlanningApi(HttpClient http) : IPlanningApi
{
    private const string Route = "api/planning";

    public Task<IReadOnlyList<PlanningFamilyListItem>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        GetListAsync<PlanningFamilyListItem>("families", cancellationToken);

    public Task<PlanningFamilyDetail?> GetFamilyAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        GetAsync<PlanningFamilyDetail>($"families/{familyGroupId}", cancellationToken);

    public async Task<Guid> CreateFamilyAsync(string name, string? description, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{Route}/families", new { name, description }, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Guid>(cancellationToken) : Guid.Empty;
    }

    public Task<IReadOnlyList<PlanningFamilySearchItem>> SearchFamiliesAsync(string query, CancellationToken cancellationToken) =>
        GetListAsync<PlanningFamilySearchItem>($"families/search?q={Uri.EscapeDataString(query)}", cancellationToken);

    public async Task<JoinRequestResult> RequestJoinAsync(Guid familyGroupId, string? message, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{Route}/families/{familyGroupId}/join-request", new { message }, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<JoinRequestResult>(cancellationToken)
            : JoinRequestResult.GroupNotFound;
    }

    public async Task<JoinDecisionResult> DecideJoinRequestAsync(Guid requestId, bool approve, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{Route}/join-requests/{requestId}/decision", new { approve }, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<JoinDecisionResult>(cancellationToken)
            : JoinDecisionResult.NotFound;
    }

    public Task<IReadOnlyList<PlanningHouseholdMember>> ListHouseholdAsync(CancellationToken cancellationToken) =>
        GetListAsync<PlanningHouseholdMember>("household", cancellationToken);

    public async Task AddHouseholdMemberAsync(string name, CancellationToken cancellationToken) =>
        await http.PostAsJsonAsync($"{Route}/household", new { name }, cancellationToken);

    public async Task<bool> RenameHouseholdMemberAsync(Guid householdMemberId, string name, CancellationToken cancellationToken) =>
        (await http.PutAsJsonAsync($"{Route}/household/{householdMemberId}", new { name }, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> RemoveHouseholdMemberAsync(Guid householdMemberId, CancellationToken cancellationToken) =>
        (await http.DeleteAsync($"{Route}/household/{householdMemberId}", cancellationToken)).IsSuccessStatusCode;

    public Task<IReadOnlyList<PlanningEventSummary>> ListUpcomingEventsAsync(CancellationToken cancellationToken) =>
        GetListAsync<PlanningEventSummary>("events/upcoming", cancellationToken);

    public Task<IReadOnlyList<PlanningEventSummary>> ListGroupEventsAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        GetListAsync<PlanningEventSummary>($"families/{familyGroupId}/events", cancellationToken);

    public Task<PlanningEventDetail?> GetEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        GetAsync<PlanningEventDetail>($"events/{eventId}", cancellationToken);

    public async Task<Guid?> CreateEventAsync(PlanningEventInput input, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{Route}/events", input, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Guid>(cancellationToken) : null;
    }

    public async Task<bool> UpdateEventAsync(Guid eventId, PlanningEventInput input, CancellationToken cancellationToken) =>
        (await http.PutAsJsonAsync($"{Route}/events/{eventId}", input, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> SetEventStatusAsync(Guid eventId, bool cancelled, CancellationToken cancellationToken) =>
        (await http.PostAsJsonAsync($"{Route}/events/{eventId}/status", new { cancelled }, cancellationToken)).IsSuccessStatusCode;

    public async Task<PlanningRsvpState> GetRsvpAsync(Guid eventId, CancellationToken cancellationToken) =>
        await GetAsync<PlanningRsvpState>($"events/{eventId}/rsvp", cancellationToken)
            ?? new PlanningRsvpState(null, null, [], [], false);

    public Task<IReadOnlyList<PlanningRsvpEntry>> ListRsvpsAsync(Guid eventId, CancellationToken cancellationToken) =>
        GetListAsync<PlanningRsvpEntry>($"events/{eventId}/rsvps", cancellationToken);

    public async Task<RsvpSaveResult> SaveRsvpAsync(
        Guid eventId,
        bool attending,
        string? note,
        IReadOnlyCollection<Guid> householdMemberIds,
        IReadOnlyList<string> guests,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"{Route}/events/{eventId}/rsvp",
            new { attending, note, householdMemberIds, guests },
            cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RsvpSaveResult>(cancellationToken)
            : RsvpSaveResult.Forbidden;
    }

    public Task<IReadOnlyList<PlanningEventPost>> ListPostsAsync(Guid eventId, CancellationToken cancellationToken) =>
        GetListAsync<PlanningEventPost>($"events/{eventId}/posts", cancellationToken);

    public async Task<PostAddResult> AddPostAsync(
        Guid eventId,
        bool announcement,
        string body,
        Guid? parentPostId,
        CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync(
            $"{Route}/events/{eventId}/posts",
            new { announcement, content = body, parentPostId },
            cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PostAddResult>(cancellationToken)
            : PostAddResult.Forbidden;
    }

    public async Task<bool> DeletePostAsync(Guid postId, CancellationToken cancellationToken) =>
        (await http.DeleteAsync($"{Route}/posts/{postId}", cancellationToken)).IsSuccessStatusCode;

    public Task<PlanningPotluckBoard?> GetPotluckAsync(Guid eventId, CancellationToken cancellationToken) =>
        GetAsync<PlanningPotluckBoard>($"events/{eventId}/potluck", cancellationToken);

    public async Task<PotluckResult> SetPotluckEnabledAsync(Guid eventId, bool enabled, CancellationToken cancellationToken) =>
        await PostResultAsync($"events/{eventId}/potluck/enabled", new { enabled }, cancellationToken);

    public async Task<PotluckResult> AddPotluckSlotAsync(
        Guid eventId,
        string name,
        string? description,
        int quantity,
        CancellationToken cancellationToken) =>
        await PostResultAsync($"events/{eventId}/potluck/slots", new { name, description, quantity }, cancellationToken);

    public async Task<PotluckResult> AddCustomPotluckItemAsync(
        Guid eventId,
        string name,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken) =>
        await PostResultAsync($"events/{eventId}/potluck/items", new { name, quantity, dishNote }, cancellationToken);

    public async Task<PotluckResult> RemovePotluckSlotAsync(Guid slotId, CancellationToken cancellationToken) =>
        await DeleteResultAsync($"potluck/slots/{slotId}", cancellationToken);

    public async Task<PotluckResult> ClaimPotluckSlotAsync(Guid slotId, int quantity, string? dishNote, CancellationToken cancellationToken) =>
        await PostResultAsync($"potluck/slots/{slotId}/claim", new { quantity, dishNote }, cancellationToken);

    public async Task<PotluckResult> UnclaimPotluckSlotAsync(Guid slotId, CancellationToken cancellationToken) =>
        await DeleteResultAsync($"potluck/slots/{slotId}/claim", cancellationToken);

    public async Task<PotluckResult> UpdatePotluckSlotAsync(
        Guid slotId,
        string name,
        string? description,
        int quantity,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync($"{Route}/potluck/slots/{slotId}", new { name, description, quantity }, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PotluckResult>(cancellationToken)
            : PotluckResult.Forbidden;
    }

    public async Task<PotluckResult> RemovePotluckClaimAsync(Guid slotId, string claimUserId, CancellationToken cancellationToken) =>
        await DeleteResultAsync($"potluck/slots/{slotId}/claims/{claimUserId}", cancellationToken);

    public async Task<PotluckResult> AssignPotluckSlotAsync(Guid slotId, string userId, int quantity, CancellationToken cancellationToken) =>
        await PostResultAsync($"potluck/slots/{slotId}/assign", new { assigneeUserId = userId, quantity }, cancellationToken);

    public Task<IReadOnlyList<PlanningNotification>> ListNotificationsAsync(int take, CancellationToken cancellationToken) =>
        GetListAsync<PlanningNotification>($"notifications?take={take}", cancellationToken);

    public async Task<int> UnreadCountAsync(CancellationToken cancellationToken)
    {
        var response = await http.GetAsync($"{Route}/notifications/unread", cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<int>(cancellationToken) : 0;
    }

    public async Task MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken) =>
        await http.PostAsync($"{Route}/notifications/{notificationId}/read", null, cancellationToken);

    public async Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken) =>
        await http.PostAsync($"{Route}/notifications/read-all", null, cancellationToken);

    public Task<IReadOnlyList<PlanningAdminUser>> ListUsersAsync(string? search, CancellationToken cancellationToken) =>
        GetListAsync<PlanningAdminUser>($"admin/users?search={Uri.EscapeDataString(search ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<PlanningAdminGroup>> ListGroupsAsync(CancellationToken cancellationToken) =>
        GetListAsync<PlanningAdminGroup>("admin/groups", cancellationToken);

    public async Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        var response = await http.PostAsync($"{Route}/admin/users/{userId}/reset-password", null, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<string>(cancellationToken) : null;
    }

    public async Task<bool> SuspendUserAsync(string userId, string? reason, CancellationToken cancellationToken) =>
        (await http.PostAsJsonAsync($"{Route}/admin/users/{userId}/suspend", new { reason }, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> ReactivateUserAsync(string userId, CancellationToken cancellationToken) =>
        (await http.PostAsync($"{Route}/admin/users/{userId}/reactivate", null, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> DeleteGroupAsync(Guid familyGroupId, CancellationToken cancellationToken) =>
        (await http.DeleteAsync($"{Route}/admin/groups/{familyGroupId}", cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> UploadEventCoverAsync(Guid eventId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "cover", "cover");
        return (await http.PostAsync($"{Route}/events/{eventId}/cover", form, cancellationToken)).IsSuccessStatusCode;
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync($"{Route}/{path}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        var response = await http.GetAsync($"{Route}/{path}", cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(cancellationToken) : null;
    }

    private async Task<PotluckResult> PostResultAsync(string path, object body, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"{Route}/{path}", body, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PotluckResult>(cancellationToken)
            : PotluckResult.Forbidden;
    }

    private async Task<PotluckResult> DeleteResultAsync(string path, CancellationToken cancellationToken)
    {
        var response = await http.DeleteAsync($"{Route}/{path}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PotluckResult>(cancellationToken)
            : PotluckResult.Forbidden;
    }
}
