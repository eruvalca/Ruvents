namespace Ruvents.UI.Planning;

/// <summary>
/// Names shared by the browser time-zone lookup, the pre-paint cookie that lets a
/// statically rendered request format in the viewer's zone, and the prerender state that
/// carries the zone into the interactive renderer.
/// </summary>
public static class ViewerTimeZoneStorage
{
    public const string CookieName = "ruvents-tz";
    public const string PersistedKey = "ruvents-viewer-zone";
}
