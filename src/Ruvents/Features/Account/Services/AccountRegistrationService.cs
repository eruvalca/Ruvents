using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

// Request-bound adapter: callers supply RequestAborted for explicit store work;
// the registered AspNetUserManager uses that request token for its tokenless APIs.
internal sealed class AccountRegistrationService(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore)
{
    public async Task<RegistrationOutcome> PasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await InitializeUserAsync(email, cancellationToken);
        var result = await userManager.CreateAsync(user, password);
        return result.Succeeded
            ? new RegistrationOutcome.Created(user)
            : new RegistrationOutcome.CreationRejected(result.Errors.ToArray());
    }

    public async Task<ExternalRegistrationOutcome> ExternalAsync(string email, ExternalLoginInfo login, CancellationToken cancellationToken)
    {
        var user = await InitializeUserAsync(email, cancellationToken);
        var creation = await userManager.CreateAsync(user);
        if (!creation.Succeeded)
        {
            return new RegistrationOutcome.CreationRejected(creation.Errors.ToArray());
        }
        var linking = await userManager.AddLoginAsync(user, login);
        return linking.Succeeded
            ? new RegistrationOutcome.Created(user)
            : new ExternalRegistrationOutcome.ExternalLoginLinkFailed(user, linking.Errors.ToArray());
    }

    private async Task<ApplicationUser> InitializeUserAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        var user = new ApplicationUser();
        await userStore.SetUserNameAsync(user, email, cancellationToken);
        await ((IUserEmailStore<ApplicationUser>)userStore).SetEmailAsync(user, email, cancellationToken);
        return user;
    }
}
