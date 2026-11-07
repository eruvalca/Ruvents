using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Replaces the profile photo on an existing account. The photo arrives either as
/// the cropped <c>PhotoData</c> payload from <c>PhotoCropField</c> or as a plain file
/// upload from the <c>&lt;noscript&gt;</c> fallback, so the required-photo gate page
/// and account management share one read, validate, and write path.
/// </summary>
internal sealed class ProfilePhotoWriter(
    IProfilePhotoStore photos,
    IProfilePhotoUploadReader uploads,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IdentityCancellation cancellation)
{
    // StatusMessage renders error styling for messages that start with "Error", which
    // is the Identity convention these pages already use.
    private const string NothingChosen = "Error: Choose a profile photo, position it, and select “Use this crop”.";

    /// <summary>
    /// Returns the crop posted with this request when it is a readable data URL, so a
    /// page that failed after the post can render the prepared photo again instead of
    /// making the user position it a second time.
    /// </summary>
    internal static string? ReadPostedPreview(HttpContext httpContext)
    {
        var cropped = httpContext.Request.HasFormContentType
            ? httpContext.Request.Form["PhotoData"].ToString()
            : string.Empty;
        return ProfilePhotoService.TryDecodeDataUrl(cropped, out _, out _, out _) ? cropped : null;
    }

    /// <summary>
    /// Stores a new photo for <paramref name="user"/> and refreshes the sign-in cookie
    /// so the profile-photo claim matches. Returns null on success, otherwise a
    /// user-facing error message.
    /// </summary>
    internal async Task<string?> TryReplaceAsync(ApplicationUser user, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var upload = await ReadAsync(httpContext, cancellationToken);
        if (upload.Error is not null)
        {
            return upload.Error;
        }

        if (!photos.TryValidate(upload.ContentType, upload.Bytes.Length, out var validationError))
        {
            return $"Error: {validationError}";
        }

        var previous = user.ProfilePhotoBlobName;
        await using (var content = new MemoryStream(upload.Bytes))
        {
            // The account already exists; the required photo must not be abandoned
            // by request cancellation.
            user.ProfilePhotoBlobName = await photos.SaveAsync(user.Id, content, upload.ContentType, CancellationToken.None);
        }

        // The blob is committed, so the account update and cookie refresh that make it
        // visible must run to completion even if the request disconnects.
        cancellation.CompleteWrite();

        user.ProfilePhotoContentType = upload.ContentType;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            // The account still references the previous blob, so keep it and report the
            // failure instead of deleting a photo the database still uses.
            return "Error: Your profile photo could not be saved. Try again.";
        }

        if (!string.IsNullOrEmpty(previous))
        {
            await photos.DeleteAsync(previous, CancellationToken.None);
        }

        await signInManager.RefreshSignInAsync(user);
        return null;
    }

    private async Task<(byte[] Bytes, string ContentType, string? Error)> ReadAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var cropped = httpContext.Request.HasFormContentType
            ? httpContext.Request.Form["PhotoData"].ToString()
            : string.Empty;
        if (!string.IsNullOrEmpty(cropped))
        {
            return ProfilePhotoService.TryDecodeDataUrl(cropped, out var bytes, out var contentType, out var error)
                ? (bytes, contentType, null)
                : ([], string.Empty, $"Error: {error}");
        }

        var upload = uploads.Read(httpContext);
        if (upload is null)
        {
            return ([], string.Empty, NothingChosen);
        }

        // Reject on the declared size and type before reading the body, so an
        // oversized upload is never buffered.
        if (!photos.TryValidate(upload.ContentType, upload.Length, out var uploadError))
        {
            return ([], string.Empty, $"Error: {uploadError}");
        }

        using var buffer = new MemoryStream();
        await using (var stream = upload.OpenReadStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
        }

        return (buffer.ToArray(), upload.ContentType, null);
    }
}
