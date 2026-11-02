using Ruvents.SharedKernel.Families;

namespace Ruvents.Features.Families.Contracts;

internal interface IFamilyAccess
{
    Task<FamilyRole?> GetRoleAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid familyGroupId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListGroupIdsAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListMemberUserIdsAsync(Guid familyGroupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListOrganizerUserIdsAsync(Guid familyGroupId, CancellationToken cancellationToken);
}
