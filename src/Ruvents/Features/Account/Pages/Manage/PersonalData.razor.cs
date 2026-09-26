using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class PersonalData
{
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
        }
    }
}
