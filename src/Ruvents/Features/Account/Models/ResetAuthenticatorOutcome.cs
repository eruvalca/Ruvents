using OneOf;

namespace Ruvents.Features.Account.Models;

[GenerateOneOf]
internal sealed partial class ResetAuthenticatorOutcome : OneOfBase<ResetAuthenticatorOutcome.ResetCompleted, ResetAuthenticatorOutcome.DisableFailed, ResetAuthenticatorOutcome.DisabledButKeyResetFailed>
{
    internal readonly record struct ResetCompleted;
    internal readonly record struct DisableFailed;
    internal readonly record struct DisabledButKeyResetFailed;
}
