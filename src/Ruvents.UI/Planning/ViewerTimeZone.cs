using Microsoft.JSInterop;

namespace Ruvents.UI.Planning;

/// <summary>
/// Reads the browser zone from JavaScript. <see cref="TimeZoneInfo.Local"/> cannot stand
/// in for it because an <c>InteractiveAuto</c> page renders on the server before it hands
/// over to the browser renderer.
/// </summary>
public sealed class ViewerTimeZone(IJSRuntime jsRuntime) : IViewerTimeZone
{
    private Task<string?>? _resolution;

    public string? ResolvedZoneId { get; private set; }

    public async ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
    {
        // A shared instance keeps concurrent callers waiting on the same lookup
        // instead of seeing an unresolved null.
        var resolution = _resolution ??= ResolveCoreAsync(cancellationToken);
        return await resolution;
    }

    private async Task<string?> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken, "./_content/Ruvents.UI/time.js");
            await using (module)
            {
                var zoneId = await module.InvokeAsync<string?>("getTimeZone", cancellationToken);
                ResolvedZoneId = string.IsNullOrWhiteSpace(zoneId) ? null : zoneId;
            }
        }
        catch (JSDisconnectedException)
        {
            // The renderer is gone; nothing will read this value again.
        }
        catch (JSException)
        {
            // Keep the event's own zone when the browser cannot report a zone.
        }

        return ResolvedZoneId;
    }
}
