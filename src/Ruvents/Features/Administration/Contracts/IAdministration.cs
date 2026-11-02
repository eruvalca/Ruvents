using Ruvents.SharedKernel.Planning;

namespace Ruvents.Features.Administration.Contracts;

internal interface IAdministration
{
    Task<IReadOnlyList<PlanningAdminUser>> ListUsersAsync(string? search, CancellationToken cancellationToken);

    Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken);

    Task<bool> SuspendAsync(string userId, string? reason, CancellationToken cancellationToken);

    Task<bool> ReactivateAsync(string userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanningAdminGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    Task<bool> DeleteGroupAsync(Guid familyGroupId, CancellationToken cancellationToken);
}
