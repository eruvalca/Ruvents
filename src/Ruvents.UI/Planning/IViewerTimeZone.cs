namespace Ruvents.UI.Planning;

/// <summary>
/// The time zone of the viewer's browser. The value cannot be known until the renderer
/// connects, so the first render of a component uses the event's own zone and switches
/// once this resolves.
/// </summary>
public interface IViewerTimeZone
{
    /// <summary>Gets the browser zone once resolved, or <see langword="null"/> before then.</summary>
    string? ResolvedZoneId { get; }

    /// <summary>Resolves the browser zone once and caches it for the lifetime of the scope.</summary>
    ValueTask<string?> ResolveAsync(CancellationToken cancellationToken);
}
