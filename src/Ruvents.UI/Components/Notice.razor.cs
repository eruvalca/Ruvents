using Microsoft.AspNetCore.Components;

namespace Ruvents.UI.Components;

public sealed partial class Notice
{
    [Parameter]
    public string? Message { get; set; }

    /// <summary>One of success, error, warning, or information.</summary>
    [Parameter]
    public string Kind { get; set; } = "information";
}
