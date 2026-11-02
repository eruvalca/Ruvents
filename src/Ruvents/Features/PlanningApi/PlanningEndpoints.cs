using System.Security.Claims;
using Ruvents.Features.Administration.Contracts;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.PlanningApi;

/// <summary>
/// JSON surface for the WebAssembly renderer. The server renderer uses
/// <see cref="ServerPlanningApi"/> directly, so both hosts expose identical behavior.
/// </summary>
internal static class PlanningEndpoints
{
    internal const string ClientHeaderName = "X-Ruvents-Client";
    internal const string ClientHeaderValue = "planning-ui";

    internal static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/planning")
            .RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                // Cookie-authenticated JSON calls need cross-site request forgery protection;
                // a header a cross-origin form post cannot set is sufficient here.
                var header = context.HttpContext.Request.Headers[ClientHeaderName].ToString();
                return string.Equals(header, ClientHeaderValue, StringComparison.Ordinal)
                    ? await next(context)
                    : Results.StatusCode(StatusCodes.Status400BadRequest);
            });

        MapFamilies(api);
        MapHousehold(api);
        MapEvents(api);
        MapRsvps(api);
        MapPosts(api);
        MapPotluck(api);
        MapNotifications(api);
        MapAdministration(api);

        return endpoints;
    }

    private static void MapFamilies(RouteGroupBuilder api)
    {
        api.MapGet("/families", async (ClaimsPrincipal user, IFamilyGroups families, CancellationToken cancellationToken) =>
            Results.Ok(await families.ListMineAsync(UserId(user), cancellationToken)));

        api.MapGet("/families/search", async (string? q, ClaimsPrincipal user, IFamilyGroups families, CancellationToken cancellationToken) =>
            Results.Ok(await families.SearchAsync(q ?? string.Empty, UserId(user), cancellationToken)));

        api.MapGet("/families/{id:guid}", async (Guid id, ClaimsPrincipal user, IFamilyGroups families, CancellationToken cancellationToken) =>
        {
            var detail = await families.GetAsync(id, UserId(user), cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        api.MapPost("/families", async (CreateFamilyBody body, ClaimsPrincipal user, IFamilyGroups families, CancellationToken cancellationToken) =>
            Results.Ok(await families.CreateAsync(UserId(user), body.Name, body.Description, cancellationToken)));

        api.MapPost("/families/{id:guid}/join-request", async (Guid id, JoinRequestBody body, ClaimsPrincipal user, IJoinRequests joinRequests, CancellationToken cancellationToken) =>
            Results.Ok(await joinRequests.RequestAsync(id, UserId(user), body.Message, cancellationToken)));

        api.MapPost("/join-requests/{id:guid}/decision", async (Guid id, DecisionBody body, ClaimsPrincipal user, IJoinRequests joinRequests, CancellationToken cancellationToken) =>
            Results.Ok(await joinRequests.DecideAsync(id, UserId(user), body.Approve, cancellationToken)));
    }

    private static void MapHousehold(RouteGroupBuilder api)
    {
        api.MapGet("/household", async (ClaimsPrincipal user, IHouseholdDirectory household, CancellationToken cancellationToken) =>
            Results.Ok(await household.ListAsync(UserId(user), cancellationToken)));

        api.MapPost("/household", async (NameBody body, ClaimsPrincipal user, IHouseholdDirectory household, CancellationToken cancellationToken) =>
        {
            await household.AddAsync(UserId(user), body.Name, cancellationToken);
            return Results.NoContent();
        });

        api.MapPut("/household/{id:guid}", async (Guid id, NameBody body, ClaimsPrincipal user, IHouseholdDirectory household, CancellationToken cancellationToken) =>
            await household.RenameAsync(id, UserId(user), body.Name, cancellationToken) ? Results.NoContent() : Results.NotFound());

        api.MapDelete("/household/{id:guid}", async (Guid id, ClaimsPrincipal user, IHouseholdDirectory household, CancellationToken cancellationToken) =>
            await household.RemoveAsync(id, UserId(user), cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapEvents(RouteGroupBuilder api)
    {
        api.MapGet("/events/upcoming", async (ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
            Results.Ok(await events.ListUpcomingAsync(UserId(user), cancellationToken)));

        api.MapGet("/families/{id:guid}/events", async (Guid id, ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
            Results.Ok(await events.ListForGroupAsync(id, UserId(user), cancellationToken)));

        api.MapGet("/events/{id:guid}", async (Guid id, ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
        {
            var detail = await events.GetAsync(id, UserId(user), cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        api.MapPost("/events", async (PlanningEventInput input, ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
            await events.CreateAsync(input, UserId(user), cancellationToken) is { } eventId
                ? Results.Ok(eventId)
                : Results.StatusCode(StatusCodes.Status403Forbidden));

        api.MapPut("/events/{id:guid}", async (Guid id, PlanningEventInput input, ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
            await events.UpdateAsync(id, input, UserId(user), cancellationToken) ? Results.NoContent() : Results.StatusCode(StatusCodes.Status403Forbidden));

        api.MapPost("/events/{id:guid}/status", async (Guid id, CancelledBody body, ClaimsPrincipal user, IEventDirectory events, CancellationToken cancellationToken) =>
        {
            var status = body.Cancelled
                ? SharedKernel.Events.EventStatus.Cancelled
                : SharedKernel.Events.EventStatus.Scheduled;
            var updated = await events.SetStatusAsync(id, UserId(user), status, cancellationToken);
            return updated ? Results.NoContent() : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        api.MapPost("/events/{id:guid}/cover", async (Guid id, HttpRequest request, ClaimsPrincipal user, IEventCovers covers, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest();
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("cover");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest();
            }

            await using var stream = file.OpenReadStream();
            return await covers.SaveAsync(id, UserId(user), stream, file.ContentType ?? "application/octet-stream", cancellationToken)
                ? Results.NoContent()
                : Results.StatusCode(StatusCodes.Status403Forbidden);
        }).DisableAntiforgery();
    }

    private static void MapRsvps(RouteGroupBuilder api)
    {
        api.MapGet("/events/{id:guid}/rsvp", async (Guid id, ClaimsPrincipal user, IRsvpDirectory rsvps, CancellationToken cancellationToken) =>
            Results.Ok(await rsvps.GetOwnAsync(id, UserId(user), cancellationToken)));

        api.MapPut("/events/{id:guid}/rsvp", async (Guid id, RsvpBody body, ClaimsPrincipal user, IRsvpDirectory rsvps, CancellationToken cancellationToken) =>
            Results.Ok(await rsvps.SaveAsync(
                id,
                UserId(user),
                body.Attending,
                body.Note,
                body.HouseholdMemberIds,
                body.Guests,
                cancellationToken)));

        api.MapGet("/events/{id:guid}/rsvps", async (Guid id, ClaimsPrincipal user, IRsvpDirectory rsvps, CancellationToken cancellationToken) =>
            Results.Ok(await rsvps.ListAsync(id, UserId(user), cancellationToken)));
    }

    private static void MapPosts(RouteGroupBuilder api)
    {
        api.MapGet("/events/{id:guid}/posts", async (Guid id, ClaimsPrincipal user, IEventPostDirectory posts, CancellationToken cancellationToken) =>
            Results.Ok(await posts.ListAsync(id, UserId(user), cancellationToken)));

        api.MapPost("/events/{id:guid}/posts", async (Guid id, PostBody body, ClaimsPrincipal user, IEventPostDirectory posts, CancellationToken cancellationToken) =>
            Results.Ok(await posts.AddAsync(
                id,
                UserId(user),
                body.Announcement ? SharedKernel.Events.EventPostKind.Announcement : SharedKernel.Events.EventPostKind.Comment,
                body.Content,
                body.ParentPostId,
                cancellationToken)));

        api.MapDelete("/posts/{id:guid}", async (Guid id, ClaimsPrincipal user, IEventPostDirectory posts, CancellationToken cancellationToken) =>
            await posts.DeleteAsync(id, UserId(user), cancellationToken) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapPotluck(RouteGroupBuilder api)
    {
        api.MapGet("/events/{id:guid}/potluck", async (Guid id, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
        {
            var board = await potluck.GetAsync(id, UserId(user), cancellationToken);
            return board is null ? Results.NotFound() : Results.Ok(board);
        });

        api.MapPost("/events/{id:guid}/potluck/enabled", async (Guid id, EnabledBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.EnableAsync(id, UserId(user), body.Enabled, cancellationToken)));

        api.MapPost("/events/{id:guid}/potluck/slots", async (Guid id, SlotBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.AddSlotAsync(id, UserId(user), body.Name, body.Description, body.Quantity, cancellationToken)));

        api.MapPost("/events/{id:guid}/potluck/items", async (Guid id, CustomItemBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.AddCustomItemAsync(id, UserId(user), body.Name, body.Quantity, body.DishNote, cancellationToken)));

        api.MapDelete("/potluck/slots/{id:guid}", async (Guid id, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.RemoveSlotAsync(id, UserId(user), cancellationToken)));

        api.MapPut("/potluck/slots/{id:guid}", async (Guid id, SlotBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.UpdateSlotAsync(id, UserId(user), body.Name, body.Description, body.Quantity, cancellationToken)));

        api.MapDelete("/potluck/slots/{id:guid}/claims/{userId}", async (Guid id, string userId, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.RemoveClaimAsync(id, userId, UserId(user), cancellationToken)));

        api.MapPost("/potluck/slots/{id:guid}/claim", async (Guid id, ClaimBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.ClaimAsync(id, UserId(user), body.Quantity, body.DishNote, cancellationToken)));

        api.MapDelete("/potluck/slots/{id:guid}/claim", async (Guid id, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.UnclaimAsync(id, UserId(user), cancellationToken)));

        api.MapPost("/potluck/slots/{id:guid}/assign", async (Guid id, AssignBody body, ClaimsPrincipal user, IPotluckBoard potluck, CancellationToken cancellationToken) =>
            Results.Ok(await potluck.AssignAsync(id, body.AssigneeUserId, UserId(user), body.Quantity, null, cancellationToken)));
    }

    private static void MapNotifications(RouteGroupBuilder api)
    {
        api.MapGet("/notifications", async (int? take, ClaimsPrincipal user, INotificationQueries notifications, CancellationToken cancellationToken) =>
            Results.Ok(await notifications.ListAsync(UserId(user), take ?? 50, cancellationToken)));

        api.MapGet("/notifications/unread", async (ClaimsPrincipal user, INotificationQueries notifications, CancellationToken cancellationToken) =>
            Results.Ok(await notifications.UnreadCountAsync(UserId(user), cancellationToken)));

        api.MapPost("/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal user, INotificationQueries notifications, CancellationToken cancellationToken) =>
        {
            await notifications.MarkReadAsync(id, UserId(user), cancellationToken);
            return Results.NoContent();
        });

        api.MapPost("/notifications/read-all", async (ClaimsPrincipal user, INotificationQueries notifications, CancellationToken cancellationToken) =>
        {
            await notifications.MarkAllReadAsync(UserId(user), cancellationToken);
            return Results.NoContent();
        });
    }

    private static void MapAdministration(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));

        admin.MapGet("/users", async (string? search, IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.ListUsersAsync(search, cancellationToken)));

        admin.MapGet("/groups", async (IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.ListGroupsAsync(cancellationToken)));

        admin.MapPost("/users/{id}/reset-password", async (string id, IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.ResetPasswordAsync(id, cancellationToken)));

        admin.MapPost("/users/{id}/suspend", async (string id, SuspendBody body, IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.SuspendAsync(id, body.Reason, cancellationToken)));

        admin.MapPost("/users/{id}/reactivate", async (string id, IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.ReactivateAsync(id, cancellationToken)));

        admin.MapDelete("/groups/{id:guid}", async (Guid id, IAdministration administration, CancellationToken cancellationToken) =>
            Results.Ok(await administration.DeleteGroupAsync(id, cancellationToken)));
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private sealed record CreateFamilyBody(string Name, string? Description);

    private sealed record JoinRequestBody(string? Message);

    private sealed record DecisionBody(bool Approve);

    private sealed record NameBody(string Name);

    private sealed record CancelledBody(bool Cancelled);

    private sealed record RsvpBody(
        bool Attending,
        string? Note,
        IReadOnlyCollection<Guid> HouseholdMemberIds,
        IReadOnlyList<string> Guests);

    private sealed record PostBody(bool Announcement, string Content, Guid? ParentPostId);

    private sealed record EnabledBody(bool Enabled);

    private sealed record SlotBody(string Name, string? Description, int Quantity);

    private sealed record CustomItemBody(string Name, int Quantity, string? DishNote);

    private sealed record ClaimBody(int Quantity, string? DishNote);

    private sealed record AssignBody(string AssigneeUserId, int Quantity);

    private sealed record SuspendBody(string? Reason);
}
