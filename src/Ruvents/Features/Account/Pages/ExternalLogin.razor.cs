using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Ruvents.Data;

namespace Ruvents.Features.Account.Pages;

public sealed partial class ExternalLogin
{
    public const string LoginCallbackAction = "LoginCallback";

    private string? _message;
    private ExternalLoginInfo? _externalLoginInfo;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? RemoteError { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    private string? Action { get; set; }

    private string? ProviderDisplayName => _externalLoginInfo?.ProviderDisplayName;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        if (RemoteError is not null)
        {
            RedirectManager.RedirectToWithStatus("Account/Login", $"Error from external provider: {RemoteError}", HttpContext);
            return;
        }

        var info = await SignInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            RedirectManager.RedirectToWithStatus("Account/Login", "Error loading external login information.", HttpContext);
            return;
        }

        _externalLoginInfo = info;

        if (HttpMethods.IsGet(HttpContext.Request.Method))
        {
            if (string.Equals(Action, LoginCallbackAction, StringComparison.Ordinal))
            {
                await OnLoginCallbackAsync();
                return;
            }

            // We should only reach this page via the login callback, so redirect back to
            // the login page if we get here some other way.
            RedirectManager.RedirectTo("Account/Login");
        }
    }

    private async Task OnLoginCallbackAsync()
    {
        if (_externalLoginInfo is null)
        {
            RedirectManager.RedirectToWithStatus("Account/Login", "Error loading external login information.", HttpContext);
            return;
        }

        // Sign in the user with this external login provider if the user already has a login.
        var result = await SignInManager.ExternalLoginSignInAsync(
            _externalLoginInfo.LoginProvider,
            _externalLoginInfo.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (result.Succeeded)
        {
            LogUserLoggedInWithExternalProvider(
                Logger,
                _externalLoginInfo.Principal.Identity?.Name,
                _externalLoginInfo.LoginProvider);
            RedirectManager.RedirectTo(ReturnUrl);
            return;
        }
        else if (result.IsLockedOut)
        {
            RedirectManager.RedirectTo("Account/Lockout");
            return;
        }

        // If the user does not have an account, then ask the user to create an account.
        if (_externalLoginInfo.Principal.HasClaim(c => string.Equals(c.Type, ClaimTypes.Email, StringComparison.Ordinal)))
        {
            Input.Email = _externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
        }
    }

    private async Task OnValidSubmitAsync()
    {
        if (_externalLoginInfo is null)
        {
            RedirectManager.RedirectToWithStatus("Account/Login", "Error loading external login information during confirmation.", HttpContext);
            return;
        }

        var emailStore = GetEmailStore();
        var user = CreateUser();

        await UserStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

        var result = await UserManager.CreateAsync(user);
        if (result.Succeeded)
        {
            result = await UserManager.AddLoginAsync(user, _externalLoginInfo);
            if (result.Succeeded)
            {
                LogUserCreatedWithExternalProvider(Logger, _externalLoginInfo.LoginProvider);

                var userId = await UserManager.GetUserIdAsync(user);
                var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = NavigationManager.GetUriWithQueryParameters(
                    NavigationManager.ToAbsoluteUri("Account/ConfirmEmail").AbsoluteUri,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["userId"] = userId, ["code"] = code });
                await EmailSender.SendConfirmationLinkAsync(user, Input.Email, HtmlEncoder.Default.Encode(callbackUrl));

                // If account confirmation is required, we need to show the link if we don't have a real email sender
                if (UserManager.Options.SignIn.RequireConfirmedAccount)
                {
                    RedirectManager.RedirectTo("Account/RegisterConfirmation", new(StringComparer.Ordinal) { ["email"] = Input.Email });
                }
                else
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, _externalLoginInfo.LoginProvider);
                    RedirectManager.RedirectTo(ReturnUrl);
                }
            }
        }
        else
        {
            _message = $"Error: {string.Join(",", result.Errors.Select(error => error.Description))}";
        }
    }

    private static ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor");
        }
    }

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!UserManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        return (IUserEmailStore<ApplicationUser>)UserStore;
    }

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "{Name} logged in with {LoginProvider} provider.")]
    private static partial void LogUserLoggedInWithExternalProvider(ILogger logger, string? name, string loginProvider);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "User created an account using {Name} provider.")]
    private static partial void LogUserCreatedWithExternalProvider(ILogger logger, string name);

    private sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }
}
