namespace Ruvents.SharedKernel.Features.Account;

public interface IAccountQueries
{
    Task<AccountSummary?> GetCurrentAsync(CancellationToken cancellationToken);
}
