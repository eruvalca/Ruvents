namespace Ruvents.Features.Account.Services;

/// <summary>
/// Deletes the planning data an account owns. Exposed as an interface so the account
/// pages depend on the behaviour rather than the database-backed implementation.
/// </summary>
internal interface IAccountDeletionData
{
    Task DeleteOwnedDataAsync(string userId, CancellationToken cancellationToken);
}
