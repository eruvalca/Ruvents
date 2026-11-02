# Ruvents

This solution contains a .NET 10 Blazor server, WebAssembly client, shared UI and
shared kernel, plus Aspire hosting and service defaults.

## Read the guidance for the task

The rules in this file apply throughout the repository; rules for a path live
beside it in `.github/instructions/NAME.instructions.md` — `ui` for components,
code-behind, styling, and rendering; `account` for Identity pages,
forms, and redirects; `server` for feature boundaries, persistence, and readiness;
`aspire` for application lifecycle, migrations, and publish; `tests` for test
selection and infrastructure; and `csharp` for C# and Razor authoring. Copilot loads
each one only when its `applyTo` glob matches a file in scope, so work that touches
no matching file — running tests, operating Aspire, publishing, authoring migrations
— loads none of them and you must read the owning file yourself. Do not preload
unrelated guidance. The pointers below and inside those files cover cross-scope
changes. Where a new rule belongs is documented in
[instruction placement](../build/agent-hooks.md#instruction-placement).
Bare paths and commands are relative to the repository root unless stated
otherwise; Markdown links are relative to their containing file.

## Execution and validation entry points

- Use Aspire as the application run/debug entry point; do not launch the web or
  WebAssembly project alone. Read the Aspire guide before lifecycle operations,
  including stopping Aspire before formatting/full builds on Windows. Ordinary
  shutdown must preserve local data; destructive cleanup requires explicit intent.
- Do not commit local credentials, hard-code ports, or make development containers
  persistent processes.
- Run relevant tests during implementation and both unit and component projects
  before completing code changes, plus affected infrastructure suites. Read the
  test guide before choosing or running them, even if no test files change.
  Headless unit/component tests do not need Aspire. The build and formatting
  requirements below still apply.
- `pwsh ./scripts/Invoke-Validation.ps1` is the local entry point: `-Scope Quick`
  checks the staged diff only, `-Scope Fast` adds formatting, the solution build,
  and unit/component tests, `-Scope Infrastructure` adds selected integration,
  Aspire, or browser suites through `-Suite`, and `-Scope Full` runs everything
  sequentially including performance cases. Prefer it over ad hoc command chains
  when you need the same coverage CI selects. Git hooks are opt-in and off by
  default; see [local validation](../build/README.md#local-validation-and-opt-in-git-hooks).

## Project boundaries and rendering

- Keep browser-compatible shared pages, components, and layouts in `Ruvents.UI`.
  Identity, persistence, and server services belong in `Ruvents`; WebAssembly
  startup belongs in `Ruvents.Client`. Preserve the dependency direction:
  `Ruvents.Client` and `Ruvents.UI` must not reference the server project.
  Keep `Ruvents.SharedKernel` usable by both server and browser code.
- SharedKernel/UI/Client project references are checked during builds
  (`RUVENTSARCH001`), and compiled dependencies are checked by
  [architecture tests](../tests/README.md#architecture-boundaries).
  Update the documented design and its checks together when intentionally changing
  them. Server feature and persistence rules live in the server guide.
- The UI is deliberately bare bones: plain HTML, CSS Grid layout, and stock
  Blazor components, with no component library or design system. Keep
  `MainLayout` and Identity navigation static, and do not pass the layout's
  `Body` across a render-mode boundary.
- Account pages use static server-side rendering (SSR). Preserve
  `[ExcludeFromInteractiveRouting]` in their page imports, authorization on
  account-management pages, and their HTTP context and cookie contracts. Keep
  form names, POST methods, `[SupplyParameterFromForm]` mapping, and antiforgery
  hooks intact. Default to static SSR with enhanced navigation; opt into
  `InteractiveAuto` where interaction needs a .NET renderer. Do not apply a global
  interactive render mode that changes account behavior. Read the account guide
  before changing those contracts, including through host startup or shared
  rendering.

## Project settings and dependencies

- Nullable reference type analysis is enabled centrally in `Directory.Build.props`.
  New projects inherit it; do not repeat or disable it in individual projects.
- NuGet package versions belong in the root `Directory.Packages.props`. Add
  versionless `PackageReference` items to consuming projects and preserve metadata
  such as `PrivateAssets`. Shared analyzer references live in `Directory.Build.props`.
- Keep SDK versions in their SDK declarations or `global.json`; they are not
  NuGet `PackageReference` versions. Do not introduce per-project version overrides
  or nested central package files without a deliberate, documented need.
- In application code, obtain the current instant from an injected `TimeProvider`,
  and pass explicit dates and instants into shared pure calculations. `RS0030`
  bans direct system-clock properties in the four application projects.
  See [build rules](../build/README.md#banned-apis) for scope.

## Validation

- Application I/O methods require an explicit final `CancellationToken` without
  an optional default. Forward the caller's token through HTTP, services, EF and
  execution-strategy retries; preserve framework override/event signatures.
  CA2016, CA1068, MA0032, MA0040, MA0079 and MA0080 are build-enforced. Explicit
  `None`/`default` needs a concrete lifetime/commit/cleanup reason, not a workaround.
- Use request cancellation for static SSR, component lifetime for interactivity,
  and host shutdown for background workers. Never serialize tokens or reuse a
  completed prerender request's token in a circuit.
- Cancellation is not rollback. Preserve uncertain-write IDs and confirmed-save
  refresh states; allow required follow-up after an irreversible Identity write.
  See [cancellation enforcement](../build/README.md#cancellation-enforcement).
- Compiler and analyzer warnings fail builds through `TreatWarningsAsErrors`.
  `.editorconfig`, `Directory.Build.props`, and `CodeMetricsConfig.txt` govern
  diagnostics. Fix findings without weakening shared settings; keep necessary
  exceptions narrow and justified. See [build rules](../build/README.md) for
  analyzer rules and the documented SDK compatibility workaround.
- Run `dotnet build Ruvents.slnx` after code changes. The Razor code-behind policy
  and the project-reference policy are checked automatically during builds; see
  `build/README.md` for diagnostics.
- For every change set, including configuration and documentation changes, run
  `dotnet format Ruvents.slnx --severity warn` from the repository root, review
  its fixes, and require the `--verify-no-changes` pass to exit zero before
  completing. Do not suppress diagnostics or exclude maintained source to obtain a
  clean pass. [build rules](../build/README.md#formatting) own the formatter's
  scope, targeted commands, and the non-C# limits it does not cover.
- After changing the policy or upgrading the SDK, also run
  `pwsh ./scripts/Test-RazorCodeBehind.ps1`. After changing project references or
  `build/Architecture.targets`, also run
  `pwsh ./scripts/Test-ArchitectureProjectReferences.ps1`.
- Fix violations rather than disabling checks or adding unrelated placeholder
  files. An empty matching partial class is valid for a markup-only component.

## Agent documentation review

- Review documentation as part of implementation work, before any authorized
  commit and before reporting completion. Update only guidance this work made
  inaccurate or missing; make no cosmetic edit just to show a review, and leave
  accurate documentation unchanged. Preserve unrelated changes and installed
  third-party skill files. In the final response, state
  `Documentation review: complete.` followed by the outcome once review is done.
  Do not report completion for a pending or blocked review.
  This does not authorize a commit or broaden a read-only task.
- Keep durable agent conventions in this file or in a path-scoped
  `.github/instructions` file, following the
  [instruction placement](../build/agent-hooks.md#instruction-placement) rules.
  Setup and runtime workflows belong in `README.md`, build rules in
  `build/README.md`, and test conventions in `tests/README.md`.
  Update existing feature and workflow documentation rather than duplicating it.
- The project-owned Copilot `userPromptSubmitted` hook supplies this reminder and
  records a workspace baseline. The companion `agentStop` hook requests at most one
  finishing review when the workspace changes during a turn. These hooks are
  advisory, not proof of documentation accuracy or a Git commit gate.
  See [agent hook maintenance](../build/agent-hooks.md) for setup and validation.
