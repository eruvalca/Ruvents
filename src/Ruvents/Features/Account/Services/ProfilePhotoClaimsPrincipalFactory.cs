using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Adds a claim when the account has a profile photo so the photo gate can decide
/// without a database read on every request. Uploading a photo refreshes the cookie.
/// </summary>
internal sealed class ProfilePhotoClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    internal const string PhotoClaimType = "ruvents:profile-photo";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrEmpty(user.ProfilePhotoBlobName))
        {
            identity.AddClaim(new Claim(PhotoClaimType, "1"));
        }

        return identity;
    }
}
