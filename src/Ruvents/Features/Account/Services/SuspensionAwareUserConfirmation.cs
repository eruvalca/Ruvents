using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Treats suspended accounts as not confirmed so the shared sign-in gate rejects
/// password, passkey, two-factor, and external sign-ins with the same result.
/// </summary>
internal sealed class SuspensionAwareUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public async Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        if (user.IsSuspended)
        {
            return false;
        }

        return !manager.Options.SignIn.RequireConfirmedAccount || await manager.IsEmailConfirmedAsync(user);
    }
}
