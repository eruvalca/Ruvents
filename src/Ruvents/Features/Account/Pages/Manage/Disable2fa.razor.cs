using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class Disable2fa
{
    private ApplicationUser? _user;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _user = await UserManager.GetUserAsync(HttpContext.User);
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        if (HttpMethods.IsGet(HttpContext.Request.Method) && !await UserManager.GetTwoFactorEnabledAsync(_user))
        {
            throw new InvalidOperationException("Cannot disable 2FA for user as it's not currently enabled.");
        }
    }

    private async Task OnSubmitAsync()
    {
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        var disable2faResult = await UserManager.SetTwoFactorEnabledAsync(_user, false);
        if (!disable2faResult.Succeeded)
        {
            throw new InvalidOperationException("Unexpected error occurred disabling 2FA.");
        }

        var userId = await UserManager.GetUserIdAsync(_user);
        LogTwoFactorDisabled(Logger, userId);
        RedirectManager.RedirectToWithStatus(
            "Account/Manage/TwoFactorAuthentication",
            "2fa has been disabled. You can reenable 2fa when you setup an authenticator app",
            HttpContext);
    }

    [LoggerMessage(EventId = 1014, Level = LogLevel.Information, Message = "User with ID '{UserId}' has disabled 2fa.")]
    private static partial void LogTwoFactorDisabled(ILogger logger, string userId);
}
