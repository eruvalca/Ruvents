using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ruvents.Data;
using Ruvents.SharedKernel.Features.Account;

namespace Ruvents.Features.Account.Services;

internal sealed class AccountQueries(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationState,
    IOptions<IdentityOptions> options) : IAccountQueries
{
    public async Task<AccountSummary?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await authenticationState.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        return await GetForUserAsync(state.User, cancellationToken);
    }

    // HTTP endpoints supply their authenticated principal; interactive components use the circuit principal above.
    internal async Task<AccountSummary?> GetForUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType);
        if (principal.Identity?.IsAuthenticated != true || userId is null)
        {
            throw new UnauthorizedAccessException("An authenticated account is required.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AccountSummary(user.UserName, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
