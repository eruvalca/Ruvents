using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.SharedKernel.Families;

namespace Ruvents.Features.Families.Services;

internal sealed class FamilyAccess(IDbContextFactory<ApplicationDbContext> dbContextFactory) : Contracts.IFamilyAccess
{
    public async Task<FamilyRole?> GetRoleAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FamilyMemberships
            .Where(membership => membership.FamilyGroupId == familyGroupId && membership.UserId == userId)
            .Select(membership => (FamilyRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FamilyMemberships
            .AnyAsync(membership => membership.FamilyGroupId == familyGroupId && membership.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListGroupIdsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FamilyMemberships
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.FamilyGroupId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListMemberUserIdsAsync(Guid familyGroupId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FamilyMemberships
            .Where(membership => membership.FamilyGroupId == familyGroupId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListOrganizerUserIdsAsync(Guid familyGroupId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FamilyMemberships
            .Where(membership => membership.FamilyGroupId == familyGroupId
                && (membership.Role == FamilyRole.Owner || membership.Role == FamilyRole.Organizer))
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);
    }
}
