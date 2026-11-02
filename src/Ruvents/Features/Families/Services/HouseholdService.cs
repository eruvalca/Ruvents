using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Families.Contracts;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Services;

internal sealed class HouseholdService(IDbContextFactory<ApplicationDbContext> dbContextFactory, TimeProvider timeProvider) : IHouseholdDirectory
{
    public async Task<IReadOnlyList<PlanningHouseholdMember>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.HouseholdMembers
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Name)
            .Select(member => new PlanningHouseholdMember(member.Id, member.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(string userId, string name, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.HouseholdMembers.Add(new HouseholdMember
        {
            UserId = userId,
            Name = name.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RenameAsync(Guid householdMemberId, string userId, string name, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.HouseholdMembers.SingleOrDefaultAsync(
            item => item.Id == householdMemberId && item.UserId == userId,
            cancellationToken);
        if (member is null)
        {
            return false;
        }

        member.Name = name.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid householdMemberId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.HouseholdMembers.SingleOrDefaultAsync(
            item => item.Id == householdMemberId && item.UserId == userId,
            cancellationToken);
        if (member is null)
        {
            return false;
        }

        db.HouseholdMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
