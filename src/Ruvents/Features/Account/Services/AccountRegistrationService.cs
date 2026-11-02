using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountRegistrationService(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore, IdentityCancellation cancellation, IAdminPromotionService adminPromotion)
{
    public async Task<RegistrationOutcome> PasswordAsync(string email, string password)
    {
        var user = await InitializeUserAsync(email);
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            cancellation.CompleteWrite();
            await adminPromotion.PromoteIfConfiguredAsync(user, cancellation.Token);
        }
        return result.Succeeded
            ? new RegistrationOutcome.Created(user)
            : new RegistrationOutcome.CreationRejected(result.Errors.ToArray());
    }

    public async Task<ExternalRegistrationOutcome> ExternalAsync(string email, ExternalLoginInfo login)
    {
        var user = await InitializeUserAsync(email);
        var creation = await userManager.CreateAsync(user);
        if (!creation.Succeeded)
        {
            return new RegistrationOutcome.CreationRejected(creation.Errors.ToArray());
        }
        cancellation.CompleteWrite();
        await adminPromotion.PromoteIfConfiguredAsync(user, cancellation.Token);
        var linking = await userManager.AddLoginAsync(user, login);
        return linking.Succeeded
            ? new RegistrationOutcome.Created(user)
            : new ExternalRegistrationOutcome.ExternalLoginLinkFailed(user, linking.Errors.ToArray());
    }

    private async Task<ApplicationUser> InitializeUserAsync(string email)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        var user = new ApplicationUser { DisplayName = CreateDisplayName(email) };
        await userStore.SetUserNameAsync(user, email, cancellation.Token);
        await ((IUserEmailStore<ApplicationUser>)userStore).SetEmailAsync(user, email, cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        return user;
    }

    private static string CreateDisplayName(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        var localPart = separator > 0 ? email[..separator] : email;
        return localPart.Length <= 100 ? localPart : localPart[..100];
    }
}
