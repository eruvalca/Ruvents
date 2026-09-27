using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountSignInService(SignInManager<ApplicationUser> signInManager)
{
    public async Task<SignInOutcome> PasswordAsync(string email, string password, bool rememberMe) =>
        Classify(await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false));

    public async Task<SignInOutcome> PasskeyAsync(string credentialJson) =>
        Classify(await signInManager.PasskeySignInAsync(credentialJson));

    public async Task<SignInOutcome> AuthenticatorAsync(string code, bool rememberMe, bool rememberMachine)
    {
        var normalizedCode = code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return Classify(await signInManager.TwoFactorAuthenticatorSignInAsync(normalizedCode, rememberMe, rememberMachine));
    }

    public async Task<SignInOutcome> RecoveryCodeAsync(string code) =>
        Classify(await signInManager.TwoFactorRecoveryCodeSignInAsync(code.Replace(" ", string.Empty, StringComparison.Ordinal)));

    public async Task<SignInOutcome> ExternalAsync(string provider, string providerKey) =>
        Classify(await signInManager.ExternalLoginSignInAsync(provider, providerKey, isPersistent: false, bypassTwoFactor: true));

    internal static SignInOutcome Classify(SignInResult result)
    {
        if (result.Succeeded)
        {
            return new SignInOutcome.Succeeded();
        }
        if (result.RequiresTwoFactor)
        {
            return new SignInOutcome.RequiresTwoFactor();
        }
        if (result.IsLockedOut)
        {
            return new SignInOutcome.LockedOut();
        }
        if (result.IsNotAllowed)
        {
            return new SignInOutcome.NotAllowed();
        }
        return new SignInOutcome.Failed();
    }
}
