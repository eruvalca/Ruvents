using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages;

public sealed partial class LoginWithRecoveryCode
{
    private string? _message;
    private ApplicationUser _user = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        // Ensure the user has gone through the username & password screen first
        _user = await SignInManager.GetTwoFactorAuthenticationUserAsync() ??
            throw new InvalidOperationException("Unable to load two-factor authentication user.");
    }

    private async Task OnValidSubmitAsync()
    {
        var recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty, StringComparison.Ordinal);

        var result = await SignInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

        var userId = await UserManager.GetUserIdAsync(_user);

        if (result.Succeeded)
        {
            LogUserLoggedInWithRecoveryCode(Logger, userId);
            RedirectManager.RedirectTo(ReturnUrl);
        }
        else if (result.IsLockedOut)
        {
            LogUserLockedOut(Logger);
            RedirectManager.RedirectTo("Account/Lockout");
        }
        else
        {
            LogInvalidRecoveryCode(Logger, userId);
            _message = "Error: Invalid recovery code entered.";
        }
    }

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "User with ID '{UserId}' logged in with a recovery code.")]
    private static partial void LogUserLoggedInWithRecoveryCode(ILogger logger, string userId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "User account locked out.")]
    private static partial void LogUserLockedOut(ILogger logger);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Invalid recovery code entered for user with ID '{UserId}' ")]
    private static partial void LogInvalidRecoveryCode(ILogger logger, string userId);

    private sealed class InputModel
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; } = "";
    }
}
