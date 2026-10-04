using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Ruvents.Features.Account.Services;
using Ruvents.SharedKernel.Features.Account;

namespace Ruvents.Features.Account.Endpoints;

internal static class AccountSummaryEndpoint
{
    internal static async Task<Results<Ok<AccountSummary>, NotFound>> HandleAsync(
        ClaimsPrincipal user, AccountQueries queries, CancellationToken cancellationToken)
    {
        var summary = await queries.GetForUserAsync(user, cancellationToken);
        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(summary);
    }
}
