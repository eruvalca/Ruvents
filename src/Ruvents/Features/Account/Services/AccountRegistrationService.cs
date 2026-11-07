using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountRegistrationService(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore, IdentityCancellation cancellation, IAdminPromotionService adminPromotion)
{
    public async Task<RegistrationOutcome> PasswordAsync(string email, string password, ProfileNameInput name)
    {
        var user = await InitializeUserAsync(email, name);
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

    private async Task<ApplicationUser> InitializeUserAsync(string email, ProfileNameInput name)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }

        var firstName = name.FirstName?.Trim() ?? string.Empty;
        var middleName = name.MiddleName?.Trim() ?? string.Empty;
        var lastName = name.LastName?.Trim() ?? string.Empty;
        var user = new ApplicationUser
        {
            FirstName = firstName,
            MiddleName = middleName,
            LastName = lastName,
            DisplayName = UserProfileNames.ResolveDisplayName(
                name.DisplayName,
                firstName,
                middleName,
                lastName,
                UserProfileNames.DisplayNameFromEmail(email)),
        };
        await userStore.SetUserNameAsync(user, email, cancellation.Token);
        await ((IUserEmailStore<ApplicationUser>)userStore).SetEmailAsync(user, email, cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        return user;
    }
}
