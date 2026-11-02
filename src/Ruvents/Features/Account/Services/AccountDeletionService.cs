using Microsoft.EntityFrameworkCore;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Removes the planning data an account owns before the account itself is deleted.
/// Identity has no foreign key to the planning tables, and a cascade cannot express
/// "remove this person's answers but keep the event", so deletion is explicit here:
/// memberships, join requests, household members, RSVPs (with their guests and
/// household attendance), potluck claims, and notifications all go, while the group
/// content they contributed to stays for the family.
/// </summary>
internal sealed class AccountDeletionService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IAccountDeletionData
{
    public async Task DeleteOwnedDataAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // The context retries transient failures, so an explicit transaction has to run
        // inside the execution strategy. Each delete is idempotent, which makes a retry safe.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Claims from an account that no longer exists would otherwise still count
            // against a slot's capacity while being hidden from the board, so the slot
            // would look empty and reject every new claim.
            await db.PotluckClaims
                .Where(claim => claim.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.Notifications
                .Where(notification => notification.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // Database cascades remove the guest and household-attendance rows.
            await db.EventRsvps
                .Where(rsvp => rsvp.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.HouseholdMembers
                .Where(member => member.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.FamilyJoinRequests
                .Where(request => request.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await db.FamilyMemberships
                .Where(membership => membership.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }
}
