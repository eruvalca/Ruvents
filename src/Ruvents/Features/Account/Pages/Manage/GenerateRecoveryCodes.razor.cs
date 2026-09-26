using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class GenerateRecoveryCodes
{
    private string? _message;
    private ApplicationUser? _user;
    private string[]? _recoveryCodes;

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

        var isTwoFactorEnabled = await UserManager.GetTwoFactorEnabledAsync(_user);
        if (!isTwoFactorEnabled)
        {
            throw new InvalidOperationException("Cannot generate recovery codes for user because they do not have 2FA enabled.");
        }
    }

    private async Task OnSubmitAsync()
    {
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        var userId = await UserManager.GetUserIdAsync(_user);
        _recoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10))?.ToArray();
        _message = "You have generated new recovery codes.";

        LogRecoveryCodesGenerated(Logger, userId);
    }

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "User with ID '{UserId}' has generated new 2FA recovery codes.")]
    private static partial void LogRecoveryCodesGenerated(ILogger logger, string userId);
}
