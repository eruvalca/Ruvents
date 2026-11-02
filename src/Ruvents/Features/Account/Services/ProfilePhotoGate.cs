namespace Ruvents.Features.Account.Services;

/// <summary>
/// Paths reachable without a profile photo: the upload page itself, the image
/// endpoint, Identity sign-out, readiness probes, and static assets.
/// </summary>
internal static class ProfilePhotoGate
{
    internal static bool IsExempt(PathString path)
    {
        if (!path.HasValue)
        {
            return true;
        }

        var value = path.Value;
        return _prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || Path.HasExtension(value);
    }

    private static readonly string[] _prefixes =
    [
        "/account/photo",
        "/account/logout",
        "/health",
        "/alive",
        "/_blazor",
        "/_framework",
        "/_content",
        "/pwa/",
        "/icons/",
    ];
}
