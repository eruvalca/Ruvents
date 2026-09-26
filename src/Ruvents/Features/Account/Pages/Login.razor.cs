using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Pages;

public sealed partial class Login
{
    private string? _errorMessage;
    private EditContext _editContext = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private LoginInputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        _editContext = new EditContext(Input);

        if (HttpMethods.IsGet(HttpContext.Request.Method))
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }
    }

    public async Task LoginUserAsync()
    {
        if (!string.IsNullOrEmpty(Input.Passkey?.Error))
        {
            _errorMessage = $"Error: {Input.Passkey.Error}";
            return;
        }

        SignInResult result;
        if (!string.IsNullOrEmpty(Input.Passkey?.CredentialJson))
        {
            // When performing passkey sign-in, don't perform form validation.
            result = await SignInManager.PasskeySignInAsync(Input.Passkey.CredentialJson);
        }
        else
        {
            // If doing a password sign-in, validate the form.
            if (!_editContext.Validate())
            {
                return;
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            result = await SignInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        }

        if (result.Succeeded)
        {
            LogUserLoggedIn(Logger);
            RedirectManager.RedirectTo(ReturnUrl);
        }
        else if (result.RequiresTwoFactor)
        {
            RedirectManager.RedirectTo(
                "Account/LoginWith2fa",
                new(StringComparer.Ordinal) { ["returnUrl"] = ReturnUrl, ["rememberMe"] = Input.RememberMe });
        }
        else if (result.IsLockedOut)
        {
            LogUserLockedOut(Logger);
            RedirectManager.RedirectTo("Account/Lockout");
        }
        else
        {
            _errorMessage = "Error: Invalid login attempt.";
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User logged in.")]
    private static partial void LogUserLoggedIn(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "User account locked out.")]
    private static partial void LogUserLockedOut(ILogger logger);
}
