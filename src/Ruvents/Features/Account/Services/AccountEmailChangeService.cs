using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Models;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountEmailChangeService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
{
    public async Task<EmailChangeOutcome> ChangeAsync(ApplicationUser user, string email, string token)
    {
        var emailChange = await userManager.ChangeEmailAsync(user, email, token);
        if (!emailChange.Succeeded)
        {
            return new EmailChangeOutcome.EmailChangeRejected(emailChange.Errors.ToArray());
        }
        var usernameChange = await userManager.SetUserNameAsync(user, email);
        if (!usernameChange.Succeeded)
        {
            return new EmailChangeOutcome.UsernameChangeRejected(usernameChange.Errors.ToArray());
        }
        await signInManager.RefreshSignInAsync(user);
        return new EmailChangeOutcome.Changed();
    }
}
