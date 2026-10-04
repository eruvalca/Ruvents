using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

// This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
// every 30 minutes an interactive circuit is connected.
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A circuit's revalidation lifetime is independent of an HTTP request or UserManager's captured token.
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
            var valid = await ValidateSecurityStampAsync(store, authenticationState.User, cancellationToken);
            // A store may have ignored cancellation. Never sign out a replacement authentication state.
            cancellationToken.ThrowIfCancellationRequested();
            return valid;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            // The .NET 10 revalidation loop recognizes TaskCanceledException with this exact token.
            throw new TaskCanceledException("Security-stamp revalidation was canceled.", exception, cancellationToken);
        }
    }

    private async Task<bool> ValidateSecurityStampAsync(IUserStore<ApplicationUser> store, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType);
        if (userId is null)
        {
            return false;
        }
        var user = await store.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }
        else if (store is not IUserSecurityStampStore<ApplicationUser> stampStore)
        {
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await stampStore.GetSecurityStampAsync(user, cancellationToken);
            return string.Equals(principalStamp, userStamp, StringComparison.Ordinal);
        }
    }
}
