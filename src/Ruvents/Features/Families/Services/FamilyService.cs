using Microsoft.EntityFrameworkCore;
using Ruvents.Data;
using Ruvents.Data.Entities;
using Ruvents.Features.Families.Contracts;
using Ruvents.SharedKernel.Families;

using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Services;

internal sealed class FamilyService(IDbContextFactory<ApplicationDbContext> dbContextFactory, TimeProvider timeProvider) : IFamilyGroups
{
    public async Task<IReadOnlyList<PlanningFamilyListItem>> ListMineAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (from membership in db.FamilyMemberships
                      join familyGroup in db.FamilyGroups on membership.FamilyGroupId equals familyGroup.Id
                      where membership.UserId == userId
                      orderby familyGroup.Name
                      select new PlanningFamilyListItem(
                          familyGroup.Id,
                          familyGroup.Name,
                          familyGroup.Description,
                          membership.Role,
                          db.FamilyMemberships.Count(other => other.FamilyGroupId == familyGroup.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningFamilyDetail?> GetAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var familyGroup = await db.FamilyGroups.SingleOrDefaultAsync(item => item.Id == familyGroupId, cancellationToken);
        if (familyGroup is null)
        {
            return null;
        }

        var role = await db.FamilyMemberships
            .Where(membership => membership.FamilyGroupId == familyGroupId && membership.UserId == userId)
            .Select(membership => (FamilyRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        List<PlanningFamilyMember> members = [];
        List<PlanningPendingJoinRequest> pending = [];

        if (role is not null)
        {
            var memberRows = await (
                from membership in db.FamilyMemberships
                join user in db.Users on membership.UserId equals user.Id
                where membership.FamilyGroupId == familyGroupId
                orderby membership.Role descending, user.DisplayName
                select new
                {
                    membership.Id,
                    membership.UserId,
                    user.DisplayName,
                    user.UserName,
                    membership.Role,
                    membership.JoinedAtUtc,
                    HasPhoto = user.ProfilePhotoBlobName != null,
                })
                .ToListAsync(cancellationToken);
            members = memberRows.ConvertAll(row => new PlanningFamilyMember(
                row.Id,
                row.UserId,
                DisplayName(row.DisplayName, row.UserName),
                row.Role,
                row.JoinedAtUtc,
                row.HasPhoto));

            if (role is FamilyRole.Owner or FamilyRole.Organizer)
            {
                var requestRows = await (
                    from request in db.FamilyJoinRequests
                    join user in db.Users on request.UserId equals user.Id
                    where request.FamilyGroupId == familyGroupId && request.Status == JoinRequestStatus.Pending
                    orderby request.RequestedAtUtc
                    select new
                    {
                        request.Id,
                        request.UserId,
                        user.DisplayName,
                        user.UserName,
                        request.Message,
                        request.RequestedAtUtc,
                    })
                    .ToListAsync(cancellationToken);
                pending = requestRows.ConvertAll(row => new PlanningPendingJoinRequest(
                    row.Id,
                    row.UserId,
                    DisplayName(row.DisplayName, row.UserName),
                    row.Message,
                    row.RequestedAtUtc));
            }
        }

        return new PlanningFamilyDetail(familyGroup.Id, familyGroup.Name, familyGroup.Description, role, members, pending);
    }

    public async Task<Guid> CreateAsync(string userId, string name, string? description, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var familyGroup = new FamilyGroup
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        };
        db.FamilyGroups.Add(familyGroup);
        db.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyGroupId = familyGroup.Id,
            UserId = userId,
            Role = FamilyRole.Owner,
            JoinedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return familyGroup.Id;
    }

    public async Task<IReadOnlyList<PlanningFamilySearchItem>> SearchAsync(string query, string userId, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        // Escape LIKE wildcards so a typed name cannot widen its own search.
        var escaped = trimmed.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        var pattern = $"%{escaped}%";

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (from familyGroup in db.FamilyGroups
                          where EF.Functions.ILike(familyGroup.Name, pattern, "\\")
                          orderby familyGroup.Name
                          select new
                          {
                              familyGroup.Id,
                              familyGroup.Name,
                              familyGroup.Description,
                              MemberCount = db.FamilyMemberships.Count(membership => membership.FamilyGroupId == familyGroup.Id),
                              IsMember = db.FamilyMemberships.Any(membership => membership.FamilyGroupId == familyGroup.Id && membership.UserId == userId),
                              HasPending = db.FamilyJoinRequests.Any(request => request.FamilyGroupId == familyGroup.Id
                                  && request.UserId == userId
                                  && request.Status == JoinRequestStatus.Pending),
                          })
            .Take(20)
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(row => new PlanningFamilySearchItem(
            row.Id,
            row.Name,
            row.Description,
            row.MemberCount,
            row.IsMember,
            row.HasPending));
    }

    private static string DisplayName(string? displayName, string? userName) =>
        !string.IsNullOrWhiteSpace(displayName) ? displayName : userName ?? "(unknown)";
}
