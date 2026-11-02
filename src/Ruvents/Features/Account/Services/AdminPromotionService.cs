using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ruvents.Data;
using Ruvents.SharedKernel.Security;

namespace Ruvents.Features.Account.Services;

internal sealed class AdminPromotionService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<PlatformAdminOptions> options) : IAdminPromotionService
{
    public async Task<bool> PromoteIfConfiguredAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var adminEmail = options.Value.AdminEmail;
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return false;
        }

        var email = await userManager.GetEmailAsync(user);
        if (!string.Equals(email, adminEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Registration never verifies that the person owns the address, so a matching
        // email alone would let whoever registers it first take the platform Admin
        // role. Promotion therefore also requires the operator to have confirmed the
        // account's email out of band.
        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (await userManager.IsInRoleAsync(user, PlatformRoles.Admin))
        {
            return false;
        }

        if (!await roleManager.RoleExistsAsync(PlatformRoles.Admin))
        {
            var creation = await roleManager.CreateAsync(new IdentityRole(PlatformRoles.Admin));
            if (!creation.Succeeded && !await roleManager.RoleExistsAsync(PlatformRoles.Admin))
            {
                return false;
            }
        }

        var result = await userManager.AddToRoleAsync(user, PlatformRoles.Admin);
        return result.Succeeded;
    }
}
