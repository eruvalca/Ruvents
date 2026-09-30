# Ruvents

This solution contains a .NET 10 Blazor server, WebAssembly client, shared UI and
shared kernel, plus Aspire hosting and service defaults.

## Project boundaries and rendering

- Keep browser-compatible shared pages, components, and layouts in `Ruvents.UI`.
  Identity, persistence, and server services belong in `Ruvents`; WebAssembly
  startup belongs in `Ruvents.Client`. Preserve the dependency direction:
  `Ruvents.Client` and `Ruvents.UI` must not reference the server project.
  Keep `Ruvents.SharedKernel` usable by both server and browser code.
- Account pages use static server-side rendering (SSR). Preserve
  `[ExcludeFromInteractiveRouting]` in their page imports, authorization on
  account-management pages, and their HTTP context and cookie contracts. Keep
  form names, POST methods, `[SupplyParameterFromForm]` mapping, and antiforgery
  hooks intact. Shared interactive pages currently opt into `InteractiveAuto`;
  do not apply a global interactive render mode that changes account behavior.
- `BlazorDisableThrowNavigationException` is enabled. After redirecting, return
  or otherwise terminate the current branch when subsequent work must not
  execute. Do not rely on navigation throwing to stop an account operation.

## Project settings and dependencies

- Nullable reference type analysis is enabled centrally in `Directory.Build.props`.
  New projects inherit it; do not repeat or disable it in individual projects.
- NuGet package versions belong in the root `Directory.Packages.props`. Add
  versionless `PackageReference` items to consuming projects and preserve metadata
  such as `PrivateAssets`. Shared analyzer references live in `Directory.Build.props`.
- Keep SDK versions in their SDK declarations or `global.json`; they are not
  NuGet `PackageReference` versions. Do not introduce per-project version overrides
  or nested central package files without a deliberate, documented need.

## Razor components

- Every page/component must have a matching `.razor.cs` partial class, including
  markup-only components. `_Imports.razor` does not need code-behind.
- Put component members and logic in code-behind. Do not add `@code` or `@functions`
  blocks. Rendering expressions and control flow remain in `.razor` markup.
- Match the generated component's namespace, class name, and generic parameters.
  `_Imports.razor` using directives do not apply to `.razor.cs` files; add the C#
  imports the code-behind needs.
- Preserve Razor routes, rendering, authorization, injection, and component
  parameter contracts when moving code. Do not edit generated C# files.
- Mark caller-supplied `[Parameter]` properties with `[EditorRequired]` when the
  component needs them to serve its purpose. Keep optional inputs optional and
  assess route, query, form, and cascading inputs separately. Use ordinary
  auto-properties, not C# `required` or `init`. This is a Razor call-site check,
  not runtime validation or a guarantee of non-null values.
- Use file-scoped namespaces and braces around C# control-flow bodies.

## CSS and layout

- Use standard CSS and Grid for structured layout and alignment. Use normal
  document flow for prose and semantic tables; positioning is appropriate for
  overlays. Use Flexbox only when there is a specific, documented benefit.
- Keep the shared baseline and reusable form, action, notice, and table patterns
  in `src/Ruvents/wwwroot/app.css`. Keep component-specific rules in adjacent
  `.razor.css` files, with narrowly scoped `::deep` selectors only when child
  component markup requires them.
- Use descriptive classes rather than framework utilities. Do not reintroduce
  Bootstrap, inline layout styles, `!important`, decorative template icons, or
  animations. Keep the baseline neutral and controls native.
- Make layouts shrink and wrap on narrow screens, preserve visible keyboard
  focus, and put form labels before their controls. Preserve semantic links,
  buttons, tables, and all Blazor/Identity form and reconnect hooks.
- Shared notices use `class="notice"` with `data-kind="success"`, `"error"`,
  `"warning"`, or `"information"`. Keep meaning in the message, not just color.

## Visibility and inheritance

- When authoring or changing classes, use the narrowest practical visibility and
  seal concrete classes unless inheritance has a purpose. Default to
  `private sealed` for nested implementation classes and `internal sealed` for
  namespace-level implementation classes; namespace-level classes cannot be
  `private`.
- Expose types across assemblies or allow inheritance when a current contract,
  framework requirement, or deliberate base-class design calls for it. Document
  the reason for analyzer exceptions on the affected type.
- Preserve generated Razor component accessibility and Blazor parameter contracts.
  Concrete components can be `public sealed partial class`; intentional base
  components remain inheritable. Assess cross-project usage before sealing a
  public type or reducing its visibility.
- S3260 and MA0053 are warnings for eligible classes; MA0053 includes public
  types. MA0053 sees inheritance only within the current project. Use a justified
  type-level exception for an intentional base class consumed by another project.
  The authoring convention also applies where the analyzers cannot enforce it.
- CA1515 is a warning for public types in executable projects. Keep its default
  exclusion of class libraries; review their exported contracts deliberately.
  `.razor.cs` files are exempt from CA1515 because generated components are public,
  but still use private implementation members and the other C# rules. Other
  required public types need a type-level `SuppressMessage` with a concrete
  justification, as on `PasskeyOperation`.

## Constructors

- Use primary constructors whenever a handwritten class or struct needs an
  explicit instance constructor, including Razor code-behind. Additional
  constructor overloads must delegate to the primary constructor with `this(...)`.
  Types that need no explicit constructor do not need an empty primary constructor.
  Static constructors are unaffected.
- Keep Blazor `[Parameter]` and `[CascadingParameter]` inputs as properties. Perform
  initialization that depends on these inputs in the appropriate lifecycle method.
- IDE0290 is an error for conversions supported by the SDK analyzer. The authoring
  requirement also applies where the analyzer cannot suggest a conversion. This
  rule does not migrate existing `@inject` or `[Inject]` property injection.

## OneOf and operation outcomes

- Use unions for meaningful alternatives that need different handling or payloads,
  or to replace fields and flags representing mutually exclusive states. Keep
  ordinary optional values, independent booleans, and adequate existing contracts
  such as simple `IdentityResult` operations. Do not introduce a universal result
  abstraction or convert every operation to OneOf.
- Prefer named, source-generated `OneOfBase` types for reusable service outcomes:
  `[GenerateOneOf] internal sealed partial class ... : OneOfBase<...>`. Use
  descriptive case records for payloads and marker structs for cases without data.
  Let the generator supply constructors and conversions. A small local helper does
  not automatically need a named union class.
- Prefer exhaustive `Match` when producing a value and `Switch` for synchronous
  side effects. Avoid routine control flow through the union's `.Value`, `IsTn`,
  or `AsTn`. `TryPick` is appropriate when deliberately separating one case and
  handling or forwarding the remaining union; direct case inspection is also
  appropriate in tests.
- Return and await task-returning `Match` for asynchronous branches
  (`Match<Task>` or `Match<Task<T>>`); let the compiler infer the type arguments
  when they are redundant under IDE0001. Never pass asynchronous lambdas to
  `Switch`, whose callbacks return `void`.
- Represent anticipated failures and partial completion accurately, including
  earlier writes that succeeded. Preserve unexpected exceptions and cancellation;
  do not catch every exception into a generic failure outcome.
- Keep outcomes close to their feature with the narrowest practical visibility.
  Share cases only when their meaning is identical. Keep transport models separate;
  do not expose OneOf directly through forms, cookies, persistence models, or HTTP
  contracts.
- Test each meaningful case, its payload, and its observable handling. For
  multi-step operations, verify that failures skip later operations and that
  partial completion does not produce complete-success messages or logs.

Examples: [PasskeySubmission](src/Ruvents/Features/Account/Models/PasskeySubmission.cs)
models mutually exclusive inputs;
[EnableAuthenticatorOutcome](src/Ruvents/Features/Account/Models/EnableAuthenticatorOutcome.cs)
distinguishes complete success from partial completion.

## Validation

- Compiler and analyzer warnings fail builds through `TreatWarningsAsErrors`.
  `.editorconfig`, `Directory.Build.props`, and `CodeMetricsConfig.txt` govern
  diagnostics. Fix findings without weakening shared settings; keep necessary
  exceptions narrow and justified. See [build/README.md](build/README.md) for
  analyzer rules and the documented SDK compatibility workaround.
- Run `dotnet build Ruvents.slnx` after code changes. The Razor code-behind policy
  is checked automatically during builds; see `build/README.md` for diagnostics.
- For every change set, including configuration and documentation changes, run
  `dotnet format Ruvents.slnx --severity warn` from the repository root and review
  its fixes. Before completion, require a clean
  `dotnet format Ruvents.slnx --severity warn --verify-no-changes` pass. This covers
  whitespace, code style (including IDE0001 name simplification), and analyzer
  fixes at warning/error severity; suggestions remain review guidance. Do not
  suppress diagnostics or exclude maintained source just to obtain a clean pass.
  Keep generated files under their owning tools' control.
  Resolve remaining findings manually without weakening `.editorconfig`. The
  formatter does not replace builds, tests, or review of non-C# files. See
  [build/README.md](build/README.md#formatting) for scope and targeted commands.
- After changing the policy or upgrading the SDK, also run
  `pwsh ./scripts/Test-RazorCodeBehind.ps1`.
- Fix violations rather than disabling checks or adding unrelated placeholder
  files. An empty matching partial class is valid for a markup-only component.

## Tests

- Run commands from the repository root so `global.json` selects the .NET SDK
  and native Microsoft.Testing.Platform (MTP) runner. Both test projects run
  headlessly; VS Code, Aspire, a browser, and a database are not required.
- Use `Ruvents.UnitTests` for server logic and `Ruvents.ComponentTests` for
  Blazor rendering and interactions with bUnit. Run relevant tests while making
  changes, then run both projects before completing code changes. Keep the full
  solution build requirement above.
- Use Shouldly exclusively for assertions in both test projects. Keep xUnit for
  test discovery and execution; use `xunit.v3.core.mtp-v2` without the xUnit
  assertion package. Do not add xUnit `Assert`, FluentAssertions, or bUnit
  assertion helpers such as `MarkupMatches`. For semantic markup checks, use
  bUnit's `CompareTo` and assert that its differences `ShouldBeEmpty()`.
  Use Shouldly assertions inside bUnit's `WaitForAssertionAsync` for asynchronous
  rendering. Preserve exact exception checks with
  `Should.Throw<T>(action).ShouldBeOfType<T>()` when derived types must fail.
- `dotnet test` restores and builds by default. Use `--no-build` only after the
  relevant code and tests have been built with the same configuration and no
  subsequent source changes. Keep coverage and reports opt-in for routine runs.
- For focused checks, use xUnit's MTP `--filter-class` or `--filter-method`
  options directly, without a `--` separator. Do not use VSTest's `--filter`
  expressions. Confirm that the selected test names and count match the intended
  scope; zero discovered or executed tests do not establish successful validation.
- Preserve `all` / `conservative` / `1x` parallel settings. Give each test its own
  mutable state; bUnit tests create and dispose a fresh `BunitContext` with
  `await using`. Shared fixtures must support concurrent access, and any opt-out
  from parallel execution needs a documented reason.
- Reuse `ConfigureAccount`, `CaptureLogs`, and the account form helpers when
  testing account components. Exercise rendered events; use `SetFormValue` and
  `SetInputValue` only to supply posted values normally provided by static SSR
  form mapping. Do not invoke private handlers or mutate cached private state
  just to increase coverage.
- Seed Bogus per instance with `UseSeed` or a locally assigned `Randomizer`;
  never set global `Randomizer.Seed`. Keep explicit values for boundaries,
  encoded tokens, and expected results.
- bUnit verifies component behavior, not real JavaScript, browser layout, or
  server/WebAssembly render-mode transitions. Those need browser validation;
  database persistence needs integration validation. Do not claim these are
  covered by the headless unit/component suites.
- Report the commands run and actual passed, failed, and skipped counts. If
  validation is blocked, state the blocker rather than claiming success. See
  [tests/README.md](tests/README.md) for runner details, reports, and conventions.

```powershell
# Run the relevant project during development.
dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj
dotnet test --project tests/Ruvents.ComponentTests/Ruvents.ComponentTests.csproj

# Example focused check.
dotnet test --project tests/Ruvents.ComponentTests/Ruvents.ComponentTests.csproj --filter-class "Ruvents.ComponentTests.Features.Counter.Pages.CounterTests"

# Run both projects before completing code changes.
dotnet test --solution Ruvents.slnx
```

## Aspire and PostgreSQL

- Aspire is the default application run/debug entry point. Use the CLI for agent
  runs and the checked-in **Aspire: Ruvents** VS Code configuration for F5 debugging;
  in Visual Studio, use `Ruvents.AppHost` as the startup project. Do not bypass
  orchestration by launching the web or WebAssembly project alone. Unit/component
  tests remain headless and do not require Aspire.
- Run Aspire CLI commands from the repository root; `aspire.config.json` selects
  `src/Ruvents.AppHost/Ruvents.AppHost.csproj`. Use
  `aspire start --launch-profile https --non-interactive`,
  `aspire wait ruvents --timeout 120 --non-interactive`, and `aspire describe`.
  Stop Aspire before formatting/full builds on Windows and when agent validation
  is finished. Ordinary shutdown uses `aspire stop`; `--force` removes persistent
  resource instances and requires explicit destructive intent for one AppHost.
  In 13.6, named volumes are preserved unless `--volumes` is also requested;
  never add either cleanup flag to routine validation.
- Keep Aspire 13.6.0, the explicitly selected preview EF integration, and
  the managed EF tool 10.0.12 aligned with the documented setup in `README.md`.
  Use `.agents/skills` as the shared skill location; retain the single user-level
  `aspire agent mcp` entry for each agent rather than adding repository duplicates.
- PostgreSQL resource `postgres` hosts `ruventsdb` (physical database `ruvents`).
  Aspire injects `ConnectionStrings:ruventsdb`. Do not restore SQLite, commit local
  credentials, hard-code ports, or make development containers persistent processes.
- Use `IDbContextFactory<ApplicationDbContext>` for independent Blazor operations
  and dispose created contexts. Preserve Identity schema version 3 and passkeys.
- `/health` includes database readiness; `/alive` checks process liveness
  independently of database availability. Preserve the bounded database
  readiness check and Aspire's monitoring of `/health`. Both endpoints are
  currently Development-only; changing their exposure is a deliberate
  deployment decision.
- Author migrations through the built-in `ruvents-migrations` resource using the
  web project's actual startup model. Keep migrations and snapshot under
  `src/Ruvents/Data/Migrations`, namespace `Ruvents.Migrations`. Rebuild after
  generating migrations. The web resource must wait for successful migration
  completion; do not add application-startup migration code or a custom worker.
- pgAdmin is an explicit-start, run-mode developer tool. See `README.md` for
  endpoint discovery, data persistence, and telemetry. PostgreSQL's **REPL**
  command opens `psql`; use `\connect ruvents` to select the application database
  and `\q` to exit. It has normal write permissions. Database reset/drop requires
  an explicit intent to delete the local data. Do not add preview browser logging
  unless explicitly requested.
- The dashboard retains run history automatically. Keep persisted dashboard data
  in its user-local default directory and out of shared artifacts: it can contain
  unredacted credentials. View or export console logs before stopping if they are
  needed in historical runs; not every console stream is captured automatically.
- A normal run leaves pgAdmin **Not started**, migrations
  **Finished**, and the web/database resources **Running / Healthy**. Inspect
  resource commands and logs before treating optional or one-shot resource states
  as failures. Use existing Identity registration/login/account management to
  exercise PostgreSQL; remove only disposable accounts created for validation.

## C# extension members

- Prefer C# 14 extension blocks for handwritten extensions. Group related members
  by receiver and concern, using internal static containers for feature-local APIs.
  Preserve existing public contracts and document any compatibility or generator
  requirement that needs classic extension-method syntax.
- Use properties for inexpensive, side-effect-free facts and methods for
  transformations, enumeration, async work, or side effects. Keep core behavior
  and factories on types we own; do not convert every static helper.
- Follow the [C# extension members skill](.agents/skills/csharp-extension-members/SKILL.md)
  for receiver/generic rules, migration checks, and compiled examples. Keep this
  repository on C# 14; do not copy preview extension indexers or SDK settings.

## Agent documentation review

- Review documentation as part of implementation work, before any authorized
  commit and before reporting completion. Update affected instructions and docs;
  leave accurate documentation unchanged. Briefly report the review outcome.
  This does not authorize a commit or broaden a read-only task.
- Keep durable agent conventions here, setup and runtime workflows in `README.md`,
  build rules in `build/README.md`, and test conventions in `tests/README.md`.
  Update existing feature and workflow documentation rather than duplicating it.
- The project-owned Codex `UserPromptSubmit` hook supplies this reminder and
  records a workspace baseline. The companion `Stop` hook requests at most one
  finishing review when the workspace changes during a turn. These hooks are
  advisory, not proof of documentation accuracy or a Git commit gate.
  See [agent hook maintenance](build/agent-hooks.md) for setup and validation.
