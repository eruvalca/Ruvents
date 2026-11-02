using Microsoft.AspNetCore.Identity;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Rejects suspended accounts on every sign-in path. SignInManager consults
/// IUserConfirmation only while SignIn.RequireConfirmedAccount is enabled, which this
/// application keeps on for exactly that reason, so every unsuspended account counts
/// as confirmed and accounts still activate without email confirmation.
/// </summary>
internal sealed class SuspensionAwareUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
        Task.FromResult(!user.IsSuspended);
}
