using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Storage;

namespace Ruvents.Features.Account.Services;

internal sealed class ProfilePhotoService(
    BlobStorage blobStorage,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider) : IProfilePhotoStore
{
    internal const string ContainerName = "profile-photos";
    internal const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>Route of the image endpoint; the placeholder SVG is served if no blob exists.</summary>
    internal static string UrlFor(string? userId) => $"/account/photo/{userId}";

    private static readonly string[] _allowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public bool TryValidate(string? contentType, long length, out string error)
    {
        if (length <= 0)
        {
            error = "Choose a profile photo.";
            return false;
        }

        if (length > MaxBytes)
        {
            error = "The profile photo must be 2 MB or smaller.";
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

    public static bool TryDecodeDataUrl(string? payload, out byte[] bytes, out string contentType, out string error)
    {
        bytes = [];
        contentType = string.Empty;
        error = string.Empty;

        const string Prefix = "data:";
        if (string.IsNullOrEmpty(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            error = "Choose a profile photo.";
            return false;
        }

        var marker = payload.IndexOf(";base64,", StringComparison.Ordinal);
        if (marker < 0)
        {
            error = "The cropped photo could not be read. Choose it again.";
            return false;
        }

        contentType = payload[Prefix.Length..marker];
        try
        {
            bytes = Convert.FromBase64String(payload[(marker + ";base64,".Length)..]);
        }
        catch (FormatException)
        {
            error = "The cropped photo could not be read. Choose it again.";
            return false;
        }

        return true;
    }

    public async Task<string> SaveAsync(string userId, Stream content, string contentType, CancellationToken cancellationToken)
    {
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
        var blobName = $"{userId}/{timeProvider.GetUtcNow().UtcTicks}{extension}";
        await container.GetBlobClient(blobName).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, cancellationToken);
        return blobName;
    }

    public async Task<(string BlobName, string ContentType)?> GetReferenceAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.Users
            .Where(user => user.Id == userId)
            .Select(user => new { user.ProfilePhotoBlobName, user.ProfilePhotoContentType })
            .SingleOrDefaultAsync(cancellationToken);
        return photo?.ProfilePhotoBlobName is null
            ? null
            : (photo.ProfilePhotoBlobName, photo.ProfilePhotoContentType ?? "application/octet-stream");
    }

    public async Task<(Stream Content, string ContentType)?> OpenAsync(string blobName, string contentType, CancellationToken cancellationToken)
    {
        var container = await blobStorage.GetContainerAsync(ContainerName, cancellationToken);
        var blob = container.GetBlobClient(blobName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return (download.Value.Content, contentType);
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        var container = await blobStorage.GetContainerAsync(ContainerName, cancellationToken);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
