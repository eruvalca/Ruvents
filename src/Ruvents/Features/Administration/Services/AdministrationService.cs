using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Features.Administration.Contracts;
using Ruvents.SharedKernel.Planning;
using Ruvents.SharedKernel.Security;

namespace Ruvents.Features.Administration.Services;

internal sealed class AdministrationService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider) : IAdministration
{
    private const string TemporaryCredentialAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

    public async Task<IReadOnlyList<PlanningAdminUser>> ListUsersAsync(string? search, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => (user.Email ?? string.Empty).Contains(term) || (user.DisplayName ?? string.Empty).Contains(term));
        }

        var users = await query.OrderBy(user => user.Email).Take(200).ToListAsync(cancellationToken);
        var result = new List<PlanningAdminUser>(users.Count);
        foreach (var user in users)
        {
            result.Add(new PlanningAdminUser(
                user.Id,
                user.Email ?? user.UserName ?? "(no email)",
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? "(unknown)" : user.DisplayName,
                await userManager.IsInRoleAsync(user, PlatformRoles.Admin),
                user.IsSuspended,
                user.SuspendedReason,
                !string.IsNullOrEmpty(user.ProfilePhotoBlobName),
                user.SuspendedAtUtc));
        }

        return result;
    }

    public async Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var temporary = RandomNumberGenerator.GetString(TemporaryCredentialAlphabet, 16);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, temporary);
        if (!result.Succeeded)
        {
            return null;
        }

        // A reset must also end existing sessions.
        await userManager.UpdateSecurityStampAsync(user);
        return temporary;
    }

    public async Task<bool> SuspendAsync(string userId, string? reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || await userManager.IsInRoleAsync(user, PlatformRoles.Admin))
        {
            return false;
        }

        user.SuspendedAtUtc = timeProvider.GetUtcNow();
        user.SuspendedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var update = await userManager.UpdateAsync(user);
        if (update.Succeeded)
        {
            // Invalidate existing cookies through the security stamp.
            await userManager.UpdateSecurityStampAsync(user);
        }

        return update.Succeeded;
    }

    public async Task<bool> ReactivateAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        user.SuspendedAtUtc = null;
        user.SuspendedReason = null;
        var update = await userManager.UpdateAsync(user);
        return update.Succeeded;
    }

    public async Task<IReadOnlyList<PlanningAdminGroup>> ListGroupsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.FamilyGroups
            .OrderBy(familyGroup => familyGroup.Name)
            .Select(familyGroup => new
            {
                familyGroup.Id,
                familyGroup.Name,
                familyGroup.CreatedAtUtc,
                MemberCount = db.FamilyMemberships.Count(membership => membership.FamilyGroupId == familyGroup.Id),
                EventCount = db.Events.Count(calendarEvent => calendarEvent.FamilyGroupId == familyGroup.Id),
            })
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.ConvertAll(row => new PlanningAdminGroup(row.Id, row.Name, row.MemberCount, row.EventCount, row.CreatedAtUtc));
    }

    public async Task<bool> DeleteGroupAsync(Guid familyGroupId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var familyGroup = await db.FamilyGroups.SingleOrDefaultAsync(item => item.Id == familyGroupId, cancellationToken);
        if (familyGroup is null)
        {
            return false;
        }

        db.FamilyGroups.Remove(familyGroup);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
