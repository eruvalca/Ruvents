using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Extensions;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountSignInService(SignInManager<ApplicationUser> signInManager, IAdminPromotionService adminPromotion)
{
    public async Task<SignInOutcome> PasswordAsync(string email, string password, bool rememberMe)
    {
        var outcome = (await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false)).ToSignInOutcome();
        await outcome.Match(
            _ => PromoteConfiguredAdminAsync(email),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        return outcome;
    }

    public async Task<SignInOutcome> PasskeyAsync(string credentialJson) =>
        (await signInManager.PasskeySignInAsync(credentialJson)).ToSignInOutcome();

    // Sign-in has already committed the cookie, so the promotion follow-up must not be
    // abandoned by request cancellation.
    private async Task PromoteConfiguredAdminAsync(string email)
    {
        var user = await signInManager.UserManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await adminPromotion.PromoteIfConfiguredAsync(user, CancellationToken.None);
        }
    }

    public async Task<SignInOutcome> AuthenticatorAsync(string code, bool rememberMe, bool rememberMachine)
    {
        var normalizedCode = code.NormalizeAuthenticatorCode();
        return (await signInManager.TwoFactorAuthenticatorSignInAsync(normalizedCode, rememberMe, rememberMachine)).ToSignInOutcome();
    }

    // The redemption boundary is owned by CancellableUserManager, which clears the scoped
    // token as soon as the code is consumed so SignInManager's completion still runs.
    public async Task<SignInOutcome> RecoveryCodeAsync(string code) =>
        (await signInManager.TwoFactorRecoveryCodeSignInAsync(code.NormalizeRecoveryCode())).ToSignInOutcome();

    public async Task<SignInOutcome> ExternalAsync(string provider, string providerKey) =>
        (await signInManager.ExternalLoginSignInAsync(provider, providerKey, isPersistent: false, bypassTwoFactor: true)).ToSignInOutcome();
}
