using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Ruvents.Features.Events.Services;

namespace Ruvents.Features.Events.Endpoints;

internal static class EventCoverEndpoints
{
    private const string PlaceholderSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 9\" role=\"img\" aria-label=\"No cover image\">" +
        "<defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
        "<stop offset=\"0\" stop-color=\"#7a3ce0\"/><stop offset=\"1\" stop-color=\"#0f9d76\"/></linearGradient></defs>" +
        "<rect width=\"16\" height=\"9\" fill=\"url(#g)\"/></svg>";

    internal static IEndpointRouteBuilder MapEventCoverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/events/{eventId:guid}/cover", async (Guid eventId, ClaimsPrincipal user, CoverImageService covers, CancellationToken cancellationToken) =>
        {
            var content = await covers.OpenAsync(eventId, UserId(user), cancellationToken);
            return content is null
                ? Results.Text(PlaceholderSvg, "image/svg+xml", statusCode: StatusCodes.Status200OK)
                : Results.File(content.Value.Content, content.Value.ContentType, enableRangeProcessing: true);
        }).RequireAuthorization();

        return endpoints;
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
