using Ruvents.Features.Account.Services;

namespace Ruvents.Features.Account.Endpoints;

internal static class ProfilePhotoEndpoints
{
    // A stored reference can outlive its blob (storage reset, deleted file). Serving a
    // placeholder keeps avatar slots intact instead of showing a broken image.
    private const string PlaceholderSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\" role=\"img\" aria-label=\"No photo\">" +
        "<defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
        "<stop offset=\"0\" stop-color=\"#7a3ce0\"/><stop offset=\"1\" stop-color=\"#f4623a\"/></linearGradient></defs>" +
        "<circle cx=\"32\" cy=\"32\" r=\"32\" fill=\"url(#g)\"/>" +
        "<circle cx=\"32\" cy=\"25\" r=\"11\" fill=\"#fff\" fill-opacity=\"0.9\"/>" +
        "<path d=\"M12 58c2-11 10-17 20-17s18 6 20 17z\" fill=\"#fff\" fill-opacity=\"0.9\"/></svg>";

    internal static IEndpointRouteBuilder MapProfilePhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/photo/{userId}", async (string userId, ProfilePhotoService photos, CancellationToken cancellationToken) =>
        {
            var reference = await photos.GetReferenceAsync(userId, cancellationToken);
            if (reference is null)
            {
                return Placeholder();
            }

            var content = await photos.OpenAsync(reference.Value.BlobName, reference.Value.ContentType, cancellationToken);
            return content is null
                ? Placeholder()
                : Results.File(content.Value.Content, content.Value.ContentType, enableRangeProcessing: true);
        }).RequireAuthorization();

        return endpoints;
    }

    private static IResult Placeholder()
    {
        // No caching so a successful upload appears immediately.
        return Results.Text(PlaceholderSvg, "image/svg+xml", statusCode: StatusCodes.Status200OK);
    }
}
