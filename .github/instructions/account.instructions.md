---
applyTo: "src/Ruvents/Features/Account/**"
---

# Account and Identity

These rules govern account pages and services, and changes elsewhere that affect
their forms, redirects, authentication, or render modes. The repository-wide rules
and the [server guidance](server.instructions.md) still apply. Before changing
account Razor, styling, components, forms, submitters, or navigation, read the
[UI guide](ui.instructions.md), including its native Identity input/submitter and
static navigation requirements. Use the
[OneOf outcome conventions](csharp.instructions.md#oneof-and-operation-outcomes)
and the repository-wide cancellation rules for account outcomes and committed
writes; consult
[cancellation enforcement](../../build/README.md#cancellation-enforcement).
Account tests also follow the [test guide](tests.instructions.md).

## Static SSR and redirect contracts

- Account pages use static server-side rendering (SSR). Preserve
  `[ExcludeFromInteractiveRouting]` in their page imports, authorization on
  account-management pages, and their HTTP context and cookie contracts. Keep
  form names, POST methods, `[SupplyParameterFromForm]` mapping, and antiforgery
  hooks intact. Account pages stay on static SSR under the repository-wide
  [render-mode rules](../copilot-instructions.md#project-boundaries-and-rendering);
  do not apply a global interactive render mode that changes account behavior.
- `BlazorDisableThrowNavigationException` is enabled. After redirecting, return
  or otherwise terminate the current branch when subsequent work must not
  execute. Do not rely on navigation throwing to stop an account operation.
- Account services in `Features/Account/Services` own the workflows whose result is a
  named outcome (`SignInOutcome`, `RegistrationOutcome`, `EmailChangeOutcome`, …), so
  a completed write stays distinguishable from a failed follow-up. Pages and the
  Identity endpoint handlers also call `UserManager`/`SignInManager` directly,
  including for Identity's own endpoint flows; both patterns are allowed, so
  preserve the existing split instead of moving every call into a service.
- Irreversible Identity writes call `IdentityCancellation.CompleteWrite()` before
  any required follow-up, so the first committed write cannot be abandoned partway
  through by request cancellation. `CancellableUserManager` forwards the scoped
  token to Identity stores; do not read `HttpContext` from an interactive circuit.
