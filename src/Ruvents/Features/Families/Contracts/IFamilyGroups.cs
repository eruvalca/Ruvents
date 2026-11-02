using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Families.Contracts;

internal interface IFamilyGroups
{
    Task<IReadOnlyList<PlanningFamilyListItem>> ListMineAsync(string userId, CancellationToken cancellationToken);

    Task<PlanningFamilyDetail?> GetAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(string userId, string name, string? description, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningFamilySearchItem>> SearchAsync(string query, string userId, CancellationToken cancellationToken);
}
