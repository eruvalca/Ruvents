using Microsoft.AspNetCore.Components;

namespace Ruvents.Components;

public sealed partial class App
{
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;
}
