using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Deletes an account together with the planning data it owns. Exposed as an interface so
/// the account pages depend on the behaviour rather than the database-backed implementation.
/// </summary>
internal interface IAccountDeletionData
{
    /// <summary>Returns false when the account deletion itself was rejected.</summary>
    Task<bool> DeleteAccountAsync(ApplicationUser user, CancellationToken cancellationToken);
}
