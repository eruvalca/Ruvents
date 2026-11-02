using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

internal interface IAdminPromotionService
{
    Task<bool> PromoteIfConfiguredAsync(ApplicationUser user, CancellationToken cancellationToken);
}
