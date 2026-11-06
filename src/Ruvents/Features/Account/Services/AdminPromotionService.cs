using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ruvents.Data;
using Ruvents.SharedKernel.Security;

namespace Ruvents.Features.Account.Services;

internal sealed class AdminPromotionService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext database,
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
            var role = new IdentityRole(PlatformRoles.Admin);
            try
            {
                var creation = await roleManager.CreateAsync(role);
                if (!creation.Succeeded && !await roleManager.RoleExistsAsync(PlatformRoles.Admin))
                {
                    return false;
                }
            }
            catch (DbUpdateException)
            {
                // A concurrent promotion, such as the administrator signing in while the
                // bootstrap job runs, may create the role between the check and the insert.
                // Detach the failed insert so the next save in this scope cannot retry it,
                // then only fail when the role is still missing.
                database.Entry(role).State = EntityState.Detached;
                if (!await roleManager.RoleExistsAsync(PlatformRoles.Admin))
                {
                    throw;
                }
            }
        }

        var result = await userManager.AddToRoleAsync(user, PlatformRoles.Admin);
        return result.Succeeded;
    }
}
