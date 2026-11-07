namespace Ruvents.UI.Planning;

/// <summary>
/// The time zone of the viewer's browser. The value cannot be known until the renderer
/// connects, so the first render of a component uses the event's own zone and switches
/// once this resolves.
/// </summary>
public interface IViewerTimeZone
{
    /// <summary>
    /// Gets the best-known zone: the browser's confirmed zone, or a provisional value
    /// carried over from an earlier request until the renderer connects.
    /// </summary>
    string? ResolvedZoneId { get; }

    /// <summary>Gets whether <see cref="ResolvedZoneId"/> came from the browser this session.</summary>
    bool IsConfirmed { get; }

    /// <summary>Resolves and confirms the browser zone, caching it for the lifetime of the scope.</summary>
    ValueTask<string?> ResolveAsync(CancellationToken cancellationToken);
}
