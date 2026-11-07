using Microsoft.AspNetCore.Components;

namespace Ruvents.Components;

public sealed partial class App
{
    /// <summary>Set by the pre-paint script in <c>App.razor</c> from the browser's zone.</summary>
    internal const string ViewerTimeZoneCookieName = "ruvents-tz";

    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    // Statically rendered pages cannot call JavaScript, so they read the zone the
    // browser reported on an earlier request. Without it they fall back to each event's
    // own zone, which is what the pages did before the cookie existed.
    private string? ViewerTimeZoneId
    {
        get
        {
            var value = HttpContext.Request.Cookies[ViewerTimeZoneCookieName];
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
}
