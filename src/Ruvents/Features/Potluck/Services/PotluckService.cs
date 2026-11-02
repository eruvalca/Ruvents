using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Notifications;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Potluck.Services;

internal sealed class PotluckService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IEventAccess eventAccess,
    IFamilyAccess familyAccess,
    INotificationPublisher notifications) : IPotluckBoard
{
    public async Task<PlanningPotluckBoard?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null)
        {
            return null;
        }

        var role = await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var slotRows = await db.PotluckSlots
            .Where(slot => slot.EventId == eventId)
            .OrderBy(slot => slot.SortOrder)
            .ThenBy(slot => slot.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var slotIds = slotRows.ConvertAll(slot => slot.Id);
        var claimRows = await (
            from claim in db.PotluckClaims
            join user in db.Users on claim.UserId equals user.Id
            where slotIds.Contains(claim.PotluckSlotId)
            orderby claim.ClaimedAtUtc
            select new
            {
                claim.PotluckSlotId,
                claim.UserId,
                user.DisplayName,
                user.UserName,
                claim.Quantity,
                claim.DishNote,
            })
            .ToListAsync(cancellationToken);

        var slots = slotRows.ConvertAll(slot =>
        {
            var claims = claimRows
                .Where(claim => claim.PotluckSlotId == slot.Id)
                .Select(claim => new PlanningPotluckClaim(
                    claim.UserId,
                    string.IsNullOrWhiteSpace(claim.DisplayName) ? claim.UserName ?? "(unknown)" : claim.DisplayName,
                    claim.Quantity,
                    claim.DishNote,
                    string.Equals(claim.UserId, userId, StringComparison.Ordinal)))
                .ToList();
            return new PlanningPotluckSlot(
                slot.Id,
                slot.Name,
                slot.Description,
                slot.QuantityNeeded,
                claims.Sum(claim => claim.Quantity),
                slot.IsCustom,
                claims);
        });

        return new PlanningPotluckBoard(
            access.PotluckEnabled,
            IsManager(access, userId, role),
            access.PotluckEnabled && access.Status == EventStatus.Scheduled,
            slots);
    }

    public async Task<PotluckResult> EnableAsync(Guid eventId, string userId, bool enabled, CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null)
        {
            return PotluckResult.NotFound;
        }

        var role = await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken);
        if (!IsManager(access, userId, role))
        {
            return PotluckResult.Forbidden;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var calendarEvent = await db.Events.SingleAsync(item => item.Id == eventId, cancellationToken);
        calendarEvent.PotluckEnabled = enabled;
        calendarEvent.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var members = await familyAccess.ListMemberUserIdsAsync(access.FamilyGroupId, cancellationToken);
        await notifications.PublishAsync(
            members.Where(id => !string.Equals(id, userId, StringComparison.Ordinal)).ToList(),
            NotificationKind.PotluckChanged,
            enabled ? "Potluck sign-up opened" : "Potluck sign-up closed",
            $"{access.Title}: potluck sign-up was {(enabled ? "opened" : "closed")}.",
            $"/events/{eventId}",
            cancellationToken);
        return PotluckResult.Ok;
    }

    public async Task<PotluckResult> AddSlotAsync(
        Guid eventId,
        string userId,
        string name,
        string? description,
        int quantityNeeded,
        CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null)
        {
            return PotluckResult.NotFound;
        }

        var role = await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken);
        if (!IsManager(access, userId, role))
        {
            return PotluckResult.Forbidden;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sortOrder = await NextSortOrderAsync(db, eventId, cancellationToken);
        db.PotluckSlots.Add(new PotluckSlot
        {
            EventId = eventId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            QuantityNeeded = Math.Clamp(quantityNeeded, 1, 100),
            CreatedByUserId = userId,
            SortOrder = sortOrder,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
        return PotluckResult.Ok;
    }

    public async Task<PotluckResult> AddCustomItemAsync(
        Guid eventId,
        string userId,
        string name,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null)
        {
            return PotluckResult.NotFound;
        }

        if (await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken) is null)
        {
            return PotluckResult.Forbidden;
        }

        if (!access.PotluckEnabled || access.Status != EventStatus.Scheduled)
        {
            return PotluckResult.Forbidden;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sortOrder = await NextSortOrderAsync(db, eventId, cancellationToken);
        var clamped = Math.Clamp(quantity, 1, 100);
        var now = timeProvider.GetUtcNow();
        var slot = new PotluckSlot
        {
            EventId = eventId,
            Name = name.Trim(),
            QuantityNeeded = clamped,
            IsCustom = true,
            CreatedByUserId = userId,
            SortOrder = sortOrder,
            CreatedAtUtc = now,
        };
        db.PotluckSlots.Add(slot);
        db.PotluckClaims.Add(new PotluckClaim
        {
            PotluckSlotId = slot.Id,
            UserId = userId,
            Quantity = clamped,
            DishNote = string.IsNullOrWhiteSpace(dishNote) ? null : dishNote.Trim(),
            ClaimedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return PotluckResult.Ok;
    }


    public Task<PotluckResult> UpdateSlotAsync(
        Guid slotId,
        string userId,
        string name,
        string? description,
        int quantityNeeded,
        CancellationToken cancellationToken) =>
        ExecuteLockedAsync(
            slotId,
            async (db, slot, token) =>
            {
                if (!await IsManagerAsync(slot.EventId, userId, token))
                {
                    return PotluckResult.Forbidden;
                }

                slot.Name = name.Trim();
                slot.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                slot.QuantityNeeded = Math.Clamp(quantityNeeded, 1, 100);
                return PotluckResult.Ok;
            },
            cancellationToken);

    public async Task<PotluckResult> RemoveSlotAsync(Guid slotId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Transactions must run inside the provider execution strategy because the
        // production context is configured with retrying enabled.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await TryLockSlotAsync(db, slotId, cancellationToken))
            {
                return PotluckResult.NotFound;
            }

            var slot = await db.PotluckSlots.SingleAsync(item => item.Id == slotId, cancellationToken);
            if (!await IsManagerAsync(slot.EventId, userId, cancellationToken))
            {
                return PotluckResult.Forbidden;
            }

            db.PotluckSlots.Remove(slot);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PotluckResult.Ok;
        });
    }

    public Task<PotluckResult> ClaimAsync(
        Guid slotId,
        string userId,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken) =>
        ExecuteLockedAsync(
            slotId,
            async (db, slot, token) =>
            {
                var access = await eventAccess.FindAsync(slot.EventId, token);
                if (access is null)
                {
                    return PotluckResult.NotFound;
                }

                if (await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, token) is null)
                {
                    return PotluckResult.Forbidden;
                }

                if (!access.PotluckEnabled || access.Status != EventStatus.Scheduled)
                {
                    return PotluckResult.Forbidden;
                }

                var requested = Math.Clamp(quantity, 1, Math.Max(1, slot.QuantityNeeded));
                var claims = await db.PotluckClaims
                    .Where(claim => claim.PotluckSlotId == slotId)
                    .ToListAsync(token);
                var existing = claims.SingleOrDefault(claim => string.Equals(claim.UserId, userId, StringComparison.Ordinal));
                var others = claims
                    .Where(claim => !string.Equals(claim.UserId, userId, StringComparison.Ordinal))
                    .Sum(claim => claim.Quantity);
                if (others + requested > slot.QuantityNeeded)
                {
                    return PotluckResult.Full;
                }

                var note = string.IsNullOrWhiteSpace(dishNote) ? null : dishNote.Trim();
                if (existing is null)
                {
                    db.PotluckClaims.Add(new PotluckClaim
                    {
                        PotluckSlotId = slotId,
                        UserId = userId,
                        Quantity = requested,
                        DishNote = note,
                        ClaimedAtUtc = timeProvider.GetUtcNow(),
                    });
                }
                else
                {
                    existing.Quantity = requested;
                    existing.DishNote = note;
                }

                return PotluckResult.Ok;
            },
            cancellationToken);

    public Task<PotluckResult> UnclaimAsync(Guid slotId, string userId, CancellationToken cancellationToken) =>
        ExecuteLockedAsync(
            slotId,
            async (db, _, token) =>
            {
                var claim = await db.PotluckClaims.SingleOrDefaultAsync(
                    item => item.PotluckSlotId == slotId && item.UserId == userId,
                    token);
                if (claim is null)
                {
                    return PotluckResult.NotFound;
                }

                db.PotluckClaims.Remove(claim);
                return PotluckResult.Ok;
            },
            cancellationToken);

    public Task<PotluckResult> RemoveClaimAsync(
        Guid slotId,
        string claimUserId,
        string actingUserId,
        CancellationToken cancellationToken) =>
        ExecuteLockedAsync(
            slotId,
            async (db, slot, token) =>
            {
                if (!await IsManagerAsync(slot.EventId, actingUserId, token))
                {
                    return PotluckResult.Forbidden;
                }

                var claim = await db.PotluckClaims.SingleOrDefaultAsync(
                    item => item.PotluckSlotId == slotId && item.UserId == claimUserId,
                    token);
                if (claim is null)
                {
                    return PotluckResult.NotFound;
                }

                db.PotluckClaims.Remove(claim);
                return PotluckResult.Ok;
            },
            cancellationToken);

    public Task<PotluckResult> AssignAsync(
        Guid slotId,
        string assigneeUserId,
        string actingUserId,
        int quantity,
        string? dishNote,
        CancellationToken cancellationToken) =>
        ExecuteLockedAsync(
            slotId,
            async (db, slot, token) =>
            {
                if (!await IsManagerAsync(slot.EventId, actingUserId, token))
                {
                    return PotluckResult.Forbidden;
                }

                var access = await eventAccess.FindAsync(slot.EventId, token);
                if (access is null || !await familyAccess.IsMemberAsync(access.FamilyGroupId, assigneeUserId, token))
                {
                    return PotluckResult.Forbidden;
                }

                var claims = await db.PotluckClaims
                    .Where(claim => claim.PotluckSlotId == slotId)
                    .ToListAsync(token);
                var existing = claims.SingleOrDefault(claim => string.Equals(claim.UserId, assigneeUserId, StringComparison.Ordinal));
                var others = claims
                    .Where(claim => !string.Equals(claim.UserId, assigneeUserId, StringComparison.Ordinal))
                    .Sum(claim => claim.Quantity);
                var requested = Math.Clamp(quantity, 1, Math.Max(1, slot.QuantityNeeded));
                if (others + requested > slot.QuantityNeeded)
                {
                    return PotluckResult.Full;
                }

                var note = string.IsNullOrWhiteSpace(dishNote) ? null : dishNote.Trim();
                if (existing is null)
                {
                    db.PotluckClaims.Add(new PotluckClaim
                    {
                        PotluckSlotId = slotId,
                        UserId = assigneeUserId,
                        Quantity = requested,
                        DishNote = note,
                        AssignedByUserId = actingUserId,
                        ClaimedAtUtc = timeProvider.GetUtcNow(),
                    });
                }
                else
                {
                    existing.Quantity = requested;
                    existing.DishNote = note;
                    existing.AssignedByUserId = actingUserId;
                }

                return PotluckResult.Ok;
            },
            cancellationToken);

    /// <summary>
    /// Runs a slot mutation inside a transaction that first takes a row lock on the slot,
    /// so concurrent claims queue instead of failing a concurrency check. The transaction
    /// runs through the provider execution strategy because retries are enabled.
    /// </summary>
    private async Task<PotluckResult> ExecuteLockedAsync(
        Guid slotId,
        Func<ApplicationDbContext, PotluckSlot, CancellationToken, Task<PotluckResult>> mutate,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // A retry starts from a clean tracker so no stale entity is reused.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await TryLockSlotAsync(db, slotId, cancellationToken))
            {
                return PotluckResult.NotFound;
            }

            var slot = await db.PotluckSlots.SingleAsync(item => item.Id == slotId, cancellationToken);
            var result = await mutate(db, slot, cancellationToken);
            if (result != PotluckResult.Ok)
            {
                return result;
            }

            slot.ClaimVersion++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PotluckResult.Ok;
        });
    }

    /// <summary>
    /// Takes a row lock on the slot so a mutation's read-modify-write is atomic.
    /// </summary>
    private static async Task<bool> TryLockSlotAsync(ApplicationDbContext db, Guid slotId, CancellationToken cancellationToken)
    {
        var locked = await db.Database
            .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"PotluckSlots\" WHERE \"Id\" = {slotId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return locked.Count > 0;
    }
    private static async Task<int> NextSortOrderAsync(ApplicationDbContext db, Guid eventId, CancellationToken cancellationToken)
    {
        var maximum = await db.PotluckSlots
            .Where(slot => slot.EventId == eventId)
            .Select(slot => (int?)slot.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        return maximum + 1;
    }

    private async Task<bool> IsManagerAsync(Guid eventId, string userId, CancellationToken cancellationToken)
    {
        var access = await eventAccess.FindAsync(eventId, cancellationToken);
        if (access is null)
        {
            return false;
        }

        return IsManager(access, userId, await familyAccess.GetRoleAsync(access.FamilyGroupId, userId, cancellationToken));
    }

    private static bool IsManager(EventAccess access, string userId, FamilyRole? role) =>
        role is FamilyRole.Owner or FamilyRole.Organizer
        || string.Equals(access.CreatedByUserId, userId, StringComparison.Ordinal);
}

