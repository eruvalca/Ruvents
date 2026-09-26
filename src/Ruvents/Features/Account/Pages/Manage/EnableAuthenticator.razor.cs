using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class EnableAuthenticator
{
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
        Justification = "The otpauth URI format provisions TOTP authenticator apps; its scheme and structure are fixed protocol syntax.")]
    private static readonly CompositeFormat _authenticatorUriFormat =
        CompositeFormat.Parse("otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6");

    private string? _message;
    private ApplicationUser? _user;
    private string? _sharedKey;
    private string? _authenticatorUri;
    private string[]? _recoveryCodes;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        _user = await UserManager.GetUserAsync(HttpContext.User);
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        await LoadSharedKeyAndQrCodeUriAsync(_user);
    }

    private async Task OnValidSubmitAsync()
    {
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        // Strip spaces and hyphens
        var verificationCode = Input.Code
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        var is2faTokenValid = await UserManager.VerifyTwoFactorTokenAsync(
            _user, UserManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            _message = "Error: Verification code is invalid.";
            return;
        }

        await UserManager.SetTwoFactorEnabledAsync(_user, true);
        var userId = await UserManager.GetUserIdAsync(_user);
        LogAuthenticatorEnabled(Logger, userId);

        _message = "Your authenticator app has been verified.";

        if (await UserManager.CountRecoveryCodesAsync(_user) == 0)
        {
            _recoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10))?.ToArray();
        }
        else
        {
            RedirectManager.RedirectToWithStatus("Account/Manage/TwoFactorAuthentication", _message, HttpContext);
        }
    }

    private async ValueTask LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        // Load the authenticator key & QR code URI to display on the form
        var unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await UserManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await UserManager.GetAuthenticatorKeyAsync(user);
        }

        _sharedKey = FormatKey(unformattedKey!);

        var email = await UserManager.GetEmailAsync(user);
        _authenticatorUri = GenerateQrCodeUri(email!, unformattedKey!);
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Lowercase grouping is only for the displayed authenticator key; the stored key and QR-code URI use the original value.")]
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private string GenerateQrCodeUri(string email, string unformattedKey)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            _authenticatorUriFormat,
            UrlEncoder.Encode("Microsoft.AspNetCore.Identity.UI"),
            UrlEncoder.Encode(email),
            unformattedKey);
    }

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "User with ID '{UserId}' has enabled 2FA with an authenticator app.")]
    private static partial void LogAuthenticatorEnabled(ILogger logger, string userId);

    private sealed class InputModel
    {
        [Required]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = "";
    }
}
