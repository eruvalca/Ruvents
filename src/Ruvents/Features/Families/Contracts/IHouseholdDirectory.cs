using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Contracts;

internal interface IHouseholdDirectory
{
    Task<IReadOnlyList<PlanningHouseholdMember>> ListAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(string userId, string name, CancellationToken cancellationToken);

    Task<bool> RenameAsync(Guid householdMemberId, string userId, string name, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(Guid householdMemberId, string userId, CancellationToken cancellationToken);
}
