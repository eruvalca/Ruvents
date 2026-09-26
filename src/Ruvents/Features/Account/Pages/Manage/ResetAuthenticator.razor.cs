using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class ResetAuthenticator
{
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private async Task OnSubmitAsync()
    {
        var user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        await UserManager.SetTwoFactorEnabledAsync(user, false);
        await UserManager.ResetAuthenticatorKeyAsync(user);
        var userId = await UserManager.GetUserIdAsync(user);
        LogAuthenticatorReset(Logger, userId);

        await SignInManager.RefreshSignInAsync(user);

        RedirectManager.RedirectToWithStatus(
            "Account/Manage/EnableAuthenticator",
            "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.",
            HttpContext);
    }

    [LoggerMessage(EventId = 1017, Level = LogLevel.Information, Message = "User with ID '{UserId}' has reset their authentication app key.")]
    private static partial void LogAuthenticatorReset(ILogger logger, string userId);
}
