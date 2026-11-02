namespace Ruvents.Features.Account.Services;

/// <summary>Cancellation for one Identity request or explicitly scoped revalidation.</summary>
internal sealed class IdentityCancellation
{
    public CancellationToken Token { get; set; }

    // Later Identity writes and the cookie refresh must complete after the first
    // irreversible write, so a follow-up cannot be abandoned mid-success.
    public void CompleteWrite() => Token = CancellationToken.None;
}
