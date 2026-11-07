using Microsoft.AspNetCore.Components;
using Ruvents.UI.Planning;

namespace Ruvents.Features.PlanningApi;

/// <summary>
/// Supplies the zone a statically rendered request can know about and carries it across
/// the prerender boundary, so the first interactive render matches the HTML already sent.
/// </summary>
internal sealed class ServerViewerTimeZone(
    IHttpContextAccessor httpContextAccessor,
    ViewerTimeZone browserTimeZone,
    PersistentComponentState persistentComponentState) : IViewerTimeZone
{
    // Set when the interactive renderer restores state written during prerendering.
    private readonly string? _persistedZoneId =
        persistentComponentState.TryTakeFromJson<string>(ViewerTimeZoneStorage.PersistedKey, out var zoneId)
            ? zoneId
            : null;

    private string? _cookieZoneId;
    private bool _initialized;

    public string? ResolvedZoneId
    {
        get
        {
            if (browserTimeZone.ResolvedZoneId is { } resolvedZoneId)
            {
                return resolvedZoneId;
            }

            EnsureInitialized();
            return _persistedZoneId ?? _cookieZoneId;
        }
    }

    public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken) =>
        browserTimeZone.ResolveAsync(cancellationToken);

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (httpContextAccessor.HttpContext is not { } context)
        {
            // An interactive render has no request; any zone arrived through the
            // prerender state that the browser lookup already reads.
            return;
        }

        _cookieZoneId = ReadCookieZoneId(context);
        if (_cookieZoneId is not null)
        {
            // A request that reaches this point is statically rendering or prerendering,
            // so this response can carry the zone forward.
            persistentComponentState.RegisterOnPersisting(PersistZoneAsync);
        }
    }

    private Task PersistZoneAsync()
    {
        persistentComponentState.PersistAsJson(ViewerTimeZoneStorage.PersistedKey, _cookieZoneId!);
        return Task.CompletedTask;
    }

    private static string? ReadCookieZoneId(HttpContext context)
    {
        var value = context.Request.Cookies[ViewerTimeZoneStorage.CookieName];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var zoneId = Uri.UnescapeDataString(value);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return zoneId;
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
