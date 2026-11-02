using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.SharedKernel.Families;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Deletes an account together with the planning data it owns, in one transaction, so a
/// rejected account deletion cannot have already destroyed the person's memberships,
/// household, RSVPs, and claims. Identity has no foreign key to the planning tables and a
/// cascade cannot express "remove this person's answers but keep the event", so the cleanup
/// is explicit. Groups and events stay with the family.
/// </summary>
internal sealed class AccountDeletionService(
    ApplicationDbContext database,
    UserManager<ApplicationUser> userManager) : IAccountDeletionData
{
    public async Task<bool> DeleteAccountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // The context retries transient failures, so an explicit transaction has to run
        // inside the execution strategy. Both writes share this context, which is the one
        // the Identity store uses, so the account delete joins the same transaction.
        var strategy = database.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            database.ChangeTracker.Clear();
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            await TransferGroupOwnershipAsync(user.Id, cancellationToken);
            await DeleteOwnedDataAsync(user.Id, cancellationToken);

            var deletion = await userManager.DeleteAsync(user);
            if (!deletion.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    /// <summary>
    /// Promotes the longest-standing remaining member when the departing account is a
    /// group's last owner or organizer. Without this the surviving family could never
    /// approve another join request, because only owners and organizers may decide.
    /// A group whose last member leaves keeps no successor; a platform admin can remove it.
    /// </summary>
    private async Task TransferGroupOwnershipAsync(string userId, CancellationToken cancellationToken)
    {
        var ledGroups = await database.FamilyMemberships
            .Where(membership => membership.UserId == userId
                && (membership.Role == FamilyRole.Owner || membership.Role == FamilyRole.Organizer))
            .Select(membership => membership.FamilyGroupId)
            .ToListAsync(cancellationToken);

        foreach (var groupId in ledGroups)
        {
            var hasAnotherOrganizer = await database.FamilyMemberships
                .AnyAsync(
                    membership => membership.FamilyGroupId == groupId
                        && membership.UserId != userId
                        && (membership.Role == FamilyRole.Owner || membership.Role == FamilyRole.Organizer),
                    cancellationToken);
            if (hasAnotherOrganizer)
            {
                continue;
            }

            var successor = await database.FamilyMemberships
                .Where(membership => membership.FamilyGroupId == groupId && membership.UserId != userId)
                .OrderBy(membership => membership.JoinedAtUtc)
                .ThenBy(membership => membership.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (successor is not null)
            {
                successor.Role = FamilyRole.Owner;
                await database.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task DeleteOwnedDataAsync(string userId, CancellationToken cancellationToken)
    {
        // Claims from an account that no longer exists would otherwise still count against a
        // slot's capacity while being hidden from the board, so the slot would look empty and
        // reject every new claim.
        await database.PotluckClaims
            .Where(claim => claim.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Notifications
            .Where(notification => notification.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // Database cascades remove the guest and household-attendance rows.
        await database.EventRsvps
            .Where(rsvp => rsvp.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.HouseholdMembers
            .Where(member => member.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.FamilyJoinRequests
            .Where(request => request.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.FamilyMemberships
            .Where(membership => membership.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
