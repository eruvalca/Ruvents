using Ruvents.UI.Planning;

namespace Ruvents.Client.Planning;

/// <summary>
/// In the browser, <see cref="TimeZoneInfo.Local"/> is already the device's own zone, so
/// the WebAssembly renderer never needs a JavaScript round trip to know it.
/// </summary>
internal sealed class LocalTimeZone : IViewerTimeZone
{
    public string? ResolvedZoneId => TimeZoneInfo.Local.Id;

    public bool IsConfirmed => true;

    public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(ResolvedZoneId);
}
