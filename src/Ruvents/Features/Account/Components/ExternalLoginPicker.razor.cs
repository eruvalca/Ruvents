using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Components;

public sealed partial class ExternalLoginPicker
{
    private AuthenticationScheme[] _externalLogins = [];

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _externalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToArray();
    }
}
