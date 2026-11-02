using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;
using Ruvents.Storage;

namespace Ruvents.Features.Events.Services;

internal sealed class CoverImageService(
    BlobStorage blobStorage,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    EventService events,
    IFamilyAccess familyAccess,
    TimeProvider timeProvider) : IEventCovers
{
    internal const string ContainerName = "event-covers";
    internal const int MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] _allowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public bool TryValidate(string? contentType, long length, out string error)
    {
        if (length <= 0)
        {
            error = "Choose an image file.";
            return false;
        }

        if (length > MaxBytes)
        {
            error = "The cover image must be 5 MB or smaller.";
            return false;
        }

        if (contentType is null || !_allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            error = "Choose a JPEG, PNG, or WebP image.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public async Task<bool> SaveAsync(Guid eventId, string userId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // The stored content type is served back from the application's origin, so the
        // write path refuses anything but an image even if a caller skips TryValidate.
        if (!_allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await db.Events.SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (calendarEvent is null
            || !await events.CanManageAsync(calendarEvent.FamilyGroupId, calendarEvent.CreatedByUserId, userId, cancellationToken))
        {
            return false;
        }

        var container = await blobStorage.GetContainerAsync(ContainerName, cancellationToken);
        string extension;
        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".png";
        }
        else if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".webp";
        }
        else
        {
            extension = ".jpg";
        }

        var blobName = $"{eventId}/{timeProvider.GetUtcNow().UtcTicks}{extension}";
        await container.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);

        var previous = calendarEvent.CoverImageBlobName;
        calendarEvent.CoverImageBlobName = blobName;
        calendarEvent.CoverImageContentType = contentType;
        calendarEvent.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(previous))
        {
            await container.GetBlobClient(previous).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        return true;
    }

    public async Task<(Stream Content, string ContentType)?> OpenAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reference = await db.Events
            .Where(item => item.Id == eventId)
            .Select(item => new { item.FamilyGroupId, item.CoverImageBlobName, item.CoverImageContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (reference?.CoverImageBlobName is null)
        {
            return null;
        }

        // Covers belong to a family's events, so serving one requires the same
        // membership the event detail page requires.
        if (!await familyAccess.IsMemberAsync(reference.FamilyGroupId, userId, cancellationToken))
        {
            return null;
        }

        // Only ever hand back an image type. A blob stored with a script-capable
        // content type must not run from the application's own origin.
        var contentType = reference.CoverImageContentType;
        if (contentType is null || !_allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var container = await blobStorage.GetContainerAsync(ContainerName, cancellationToken);
        var blob = container.GetBlobClient(reference.CoverImageBlobName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return (download.Value.Content, contentType);
    }
}
