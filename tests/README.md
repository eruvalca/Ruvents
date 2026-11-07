# Testing

All five test projects target .NET 10 and use xUnit with native
Microsoft.Testing.Platform (MTP) integration. Open the repository root so both
the CLI and editor find `global.json` and `Ruvents.slnx`.

| Project | Scope |
| --- | --- |
| `Ruvents.UnitTests` | Account services and extensions, outcome decoding, redirects, authentication-state revalidation, Identity endpoint behavior, and service defaults. |
| `Ruvents.ComponentTests` | Account sign-in and management workflows, validation, shared account components, navigation, error request IDs, and the shared UI's `Counter`, using bUnit. |
| `Ruvents.IntegrationTests` | In-process integration with real PostgreSQL via Testcontainers (Docker required): migrations, EF/Identity stores, persistence across contexts, dependency behavior, account-deletion cleanup, and family-scale scenarios — 15 siblings with four household members each, RSVP volume, concurrent potluck claims, multi-level discussion, deadlines and permissions, 120-event lists, and administration cleanup. |
| `Ruvents.AspireIntegrationTests` | The real AppHost, database/migration startup dependencies, readiness, resource endpoints, and cross-process HTTP behavior. |
| `Ruvents.PlaywrightTests` | Real Chromium: responsive navigation that never scrolls sideways, counter interaction, document continuity, progressive-web-app installation, consent-based updates and the offline fallback, and axe heading-order/color-contrast checks in both the light and dark palettes with a 16px text-entry minimum. |

`Ruvents.Testing` is a shared support library, not a test project. It configures
isolated AppHost builders for the Aspire and Playwright projects.

The CI pipeline checks formatting and tooling on every pull request and `main`
push. Pull-request changed paths select application build/tests and
PostgreSQL/Aspire checks; documentation-only changes skip that expensive work.
Browser jobs are never path-selected or required. Every `main` push also validates
the publish artifacts; this template publishes but does not deploy. The full
non-performance browser suite and the performance cases run daily at 06:17 UTC and
on demand in
[Nightly browser and performance validation](../.github/workflows/nightly-browser.yml).
`scripts/Get-ValidationScope.ps1` is shared by CI and the local hooks described in
the [build rules](../build/README.md#local-validation-and-opt-in-git-hooks).
On its Ubuntu runner, it exports the test user's public HTTPS development
certificate, adds it to the system CA store and Chromium's NSS database, and
verifies both before testing. Only the public certificate is imported, using
NSS's `P,,` trust for this self-signed server certificate.
Creating the certificate alone does not trust it: Aspire's HTTPS readiness probes
then fail with `UntrustedRoot`, preventing every AppHost-backed test from starting.
Browser contexts accepting development certificates do not affect those probes.
They also do not bypass Chromium's service-worker certificate checks. System-only
trust lets Aspire start and ordinary pages load, but PWA worker registration
fails. Chromium 146+ defaults to `$HOME/.local/share/pki/nssdb`, retaining
`$HOME/.pki/nssdb` when that legacy database exists. The workflow handles both.
See [Chromium's Linux certificate guidance](https://chromium.googlesource.com/chromium/src/+/HEAD/docs/linux/cert_management.md).
PWA tests observe the application's own registration rejection and report it
directly instead of waiting only for a controller timeout; they do not register
a replacement worker on the application's behalf. Their browser contexts use
normal TLS validation so missing trust fails at navigation as well.

CI retains TRX results for seven days and uploads browser screenshots/traces on
failure. These diagnostics come from disposable test accounts and may include
their cookies and form data; keep them within the repository's artifact access
controls. Do not upload development browser profiles or Aspire dashboard state.
Local reports remain opt-in.

### Browser validation policy

Every application pull-request and `main` run retains all unit/component tests, the
PostgreSQL suite, and Aspire startup checks. Browser coverage is deliberately
opt-in:

- Changed paths, drafts, pull-request commits, `main` pushes, and merge groups
  never automatically select a browser job.
- Applying `run-full-browser` to a ready pull request runs the full Chromium
  suite excluding `Category=Performance`, without rerunning unrelated jobs. New
  commits also request it while the label remains. Removing the label stops
  future requested runs. The check is never required for merging.
- The `BrowserSmoke` trait remains available for focused local runs, not as a CI
  gate. Documentation-only changes skip application validation as before;
  unknown paths and PR file listings that reach GitHub's 3,000-file cap still
  conservatively select non-browser application validation.
- The nightly workflow runs the full non-performance suite and all
  `Category=Performance` cases daily. Failures on `main`, including failed
  performance regression assertions, create or comment on one rolling issue per
  suite. Issues link to the run and commit but do not copy diagnostic payloads;
  traces, screenshots, cookies, and form data remain access-controlled artifacts.
  Browser and performance test jobs have no issue-write permission.

`scripts/Test-BrowserWorkflowPolicy.ps1` asserts these properties against the
workflow files, so change the workflows and that test together. The
`run-full-browser` composite and the opt-in pull-request workflow exclude
`Category=Performance`; the nightly workflow runs only those cases in its
performance job, daily and on manual dispatch. A failed regression or correctness
assertion produces the same performance issue; the workflow does not maintain a
separate statistical baseline or trend history. Unfiltered local solution runs
still execute every test.

This template currently ships no `Category=Performance` cases, so the nightly
performance selection reports "Zero tests ran". That step ignores only exit code 8
(no tests matched) and still fails on any real failure; add performance cases with
the trait before relying on that job for measurements.

## Supported stack

Existing stack selected September 26, 2026; infrastructure/browser additions
verified October 2, 2026:

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.401 |
| bunit (component project only) | 2.13.3 |
| xunit.v3.core.mtp-v2 | 4.0.2 |
| Shouldly | 4.3.0 |
| NSubstitute (account tests) | 6.2.0 |
| Bogus (locally seeded fake data) | 35.6.5 |
| Microsoft.Testing.Platform / Platform.MSBuild | 2.5.2 |
| Microsoft.Testing.Extensions.TrxReport | 2.5.2 |
| Microsoft.Testing.Extensions.CodeCoverage | 18.12.0 |
| xunit.analyzers | 2.2.0 |
| Aspire.Hosting.Testing (matches AppHost) | 13.6.4 |
| Testcontainers.PostgreSql | 4.16.0 |
| Microsoft.Playwright | 1.64.1 |
| Deque.AxeCore.Playwright (accessibility) | 4.14.0 |
| C# Dev Kit (stable baseline) | 3.40.210 |

Package versions are centralized in `Directory.Packages.props`. Explicit MTP
runtime and MSBuild references advance xUnit's transitive 2.5.1 dependencies to
2.5.2. Test package references use `PrivateAssets="all"`. All test projects
reference xUnit's core MTP package, which supplies the framework and runner
without `xunit.v3.assert`; xUnit analyzers remain included by the root build props.

The root build props recognize the five test-project name suffixes before
evaluating shared analyzer references. Tests inherit nullable analysis, code style
rules, and warnings-as-errors. xUnit test classes are public and sealed; their
type-level CA1515 suppression documents the discovery requirement. The server
grants unit, component, and focused integration tests access to its internal types.
Aspire/browser tests interact across process boundaries instead of accessing server DI.
The component project uses the Razor SDK and references both `Ruvents.UI` and the
server project. The server also grants `DynamicProxyGenAssembly2` internal access
so NSubstitute can proxy Identity dependencies closed over the internal
`ApplicationUser` type.

`global.json` selects native .NET 10 MTP mode. Do not add VSTest packages
(`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`) or the legacy
`TestingPlatformDotnetTestSupport` bridge to these projects. bUnit provides the
component renderer and comparison tools; xUnit and MTP provide discovery and
execution, and Shouldly is the exclusive assertion library.

Use Bogus for realistic generated fixture values with a per-instance seed
(`UseSeed` or a locally assigned `Randomizer`). Do not set the global
`Randomizer.Seed`: test methods and theory rows can run in parallel.
Keep explicit literals for boundaries, encoded tokens, and expected results.

Account tests use `context.ConfigureAccount()` and `context.CaptureLogs<TComponent>()`
from `BunitAccountExtensions`. The resulting `AccountTestContext` holds each test's
HTTP context and Identity dependencies. `ComponentFormExtensions` supplies
`SetFormValue` and `SetInputValue` where static SSR form mapping needs to be simulated;
`LoggerTestExtensions.GetLoggedEventIds()` inspects captured logging calls. Keep
these receiver-focused helpers in the account test namespace and use a fresh,
asynchronously disposed `BunitContext` for every test.

## Build and run

Run these commands from the repository root:

```powershell
dotnet build Ruvents.slnx
dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj
dotnet test --project tests/Ruvents.ComponentTests/Ruvents.ComponentTests.csproj

# Install after building; repeat after updating Microsoft.Playwright.
pwsh ./tests/Ruvents.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --project tests/Ruvents.IntegrationTests/Ruvents.IntegrationTests.csproj
dotnet test --project tests/Ruvents.AspireIntegrationTests/Ruvents.AspireIntegrationTests.csproj
dotnet test --project tests/Ruvents.PlaywrightTests/Ruvents.PlaywrightTests.csproj

# Browser suites are selected by trait; the PR label and nightly job use these.
dotnet test --project tests/Ruvents.PlaywrightTests/Ruvents.PlaywrightTests.csproj --filter-trait Category=BrowserSmoke
dotnet test --project tests/Ruvents.PlaywrightTests/Ruvents.PlaywrightTests.csproj --filter-not-trait Category=Performance

# All five projects; requires the infrastructure/browser prerequisites below.
dotnet test --solution Ruvents.slnx --no-build --report-trx --coverage --coverage-output-format cobertura --results-directory TestResults
```

TRX and coverage are opt-in. Reports go into the ignored `TestResults` directory;
there is no coverage percentage gate. Use MTP/xUnit filtering options rather than
VSTest flags. This deliberately unmatched filter must fail instead of silently
passing an empty run:

```powershell
dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj --no-build --filter-method NoSuchTestMustNotExist
```

MTP reports exit code 8 (zero tests); the outer `dotnet test` command returns a
nonzero exit code.

## Configuration

### Architecture boundaries

The architecture policy has two enforcement points: MSBuild rejects forbidden
evaluated project references during builds, and ArchUnitNET rejects forbidden
compiled type dependencies during tests.

[`AssemblyDependencyTests`](Ruvents.UnitTests/Architecture/AssemblyDependencyTests.cs)
loads SharedKernel, UI, and Client once. Shouldly reports the descriptions of any
violations. Subject selections must be nonempty, and forbidden targets include
referenced types from libraries not loaded as subjects. No xUnit assertion adapter
or production package reference is needed.

Both project-reference and compiled application-dependency checks enforce:

| Subject | Allowed dependencies on other Ruvents assemblies | Additional restrictions |
| --- | --- | --- |
| `Ruvents.SharedKernel` | None | No ASP.NET Core or JS interop types; no server infrastructure listed below. |
| `Ruvents.UI` | `Ruvents.SharedKernel` | No server infrastructure listed below. |
| `Ruvents.Client` | `Ruvents.UI`, `Ruvents.SharedKernel` | No server infrastructure listed below. |

An assembly may use its own types. The application dependency rules recognize
`Ruvents` and `Ruvents.*` assembly names, so newly referenced application
assemblies must be deliberately allowed. The server-infrastructure restriction
covers the `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity`,
`Npgsql`, and `Aspire` namespace trees. Shared UI can still use Blazor
authorization APIs; `Microsoft.AspNetCore.Components.Authorization` is not server
Identity. These are specific restrictions, not a complete guarantee of browser
compatibility.

The build check also rejects unused references and references supplied through
imports or active conditions. Its sole exception is the existing Razor validator
at `build/Ruvents.Build/Ruvents.Build.csproj`, with
`ReferenceOutputAssembly="false"`, in Razor projects. This exception adds no
application dependency. The target compares normalized paths, leaves
server/hosting references unrestricted, and does not impose a NuGet package
allowlist. See the [build policy](../build/README.md#architecture-checks)
and its `Test-ArchitectureProjectReferences.ps1` regression script.

[`ServerDependencyTests`](Ruvents.UnitTests/Architecture/ServerDependencyTests.cs)
loads the server and enforces three additional design decisions:

- **Services own database access.** Direct use of EF Core, Npgsql, ADO.NET
  (`System.Data`), SQL Server client APIs, or `ApplicationDbContext` (the only
  `DbContext` in the compiled model) belongs in
  `Ruvents.Features.<Feature>.Services`/`Data`, shared `Ruvents.Data`,
  `Ruvents.Migrations`, or the actual `Program` entry point for startup/readiness
  wiring. Components are checked regardless of namespace; feature `Endpoints`
  namespaces are checked explicitly. Pages and HTTP handlers call services.
  Identity's `UserManager`/`SignInManager` APIs remain allowed. This does not
  require a repository layer or prohibit services from using EF directly.
- **Cross-feature dependencies use contracts.** Every top-level server feature,
  including new ones, may depend on another feature only through
  `Ruvents.Features.<OtherFeature>.Contracts` and its descendants. Contracts must
  not depend on implementation types in any feature, including their own;
  exposing a service/entity through a contract would defeat the boundary.
  Introduce contracts only when a consumer needs them. There are currently no
  cross-feature contracts, so that additional check runs when they exist;
  feature discovery and each selected subject set must still be nonempty.
  Server-only contracts stay on the server. Common infrastructure and startup
  composition remain outside the feature-to-feature restriction.
- **Top-level features have no dependency cycles.** Each whole
  `Ruvents.Features.<Feature>` subtree is explicitly assigned to one ArchUnitNET
  slice. Contracts count: `Account -> Errors -> Account` fails even if both
  edges use contracts. Cycles among internal namespaces such as a feature's
  `Models` and `Services` are allowed. Grouping is automatic, with no feature
  name allowlist to update when a feature is added.

Run only architecture checks with the repository's native MTP/xUnit syntax:

```powershell
dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj --configuration Debug --filter-class "Ruvents.UnitTests.Architecture.*"
```

The normal unit suite includes them automatically. For application changes, CI
additionally runs this focused Debug check and exports its TRX report, then keeps
the Release build and functional test layers. Debug preserves dependencies that compilation
optimizations can remove. Compiled architecture violations fail tests and CI;
project-reference violations fail standalone builds with `RUVENTSARCH001`.

For example, a shared component acquiring an EF `DbContext` dependency fails
the infrastructure rule with the dependent type in its failure description.
Move persistence behind a shared contract and keep its implementation on the
server. Do not bypass a failure with a skip, a blanket exclusion, or an empty
selection. If the dependency is intentional, review the boundary change and
update its narrow rule and rationale together.

The compiled checks see type signatures and method-body dependencies. They do not
inspect string-based reflection, runtime DI registrations, JavaScript, Razor render
modes, or database behavior. Endpoint classification follows the feature
`Endpoints` convention; an inline handler in `Program` remains inside its startup
exception and needs review. Shared infrastructure is trusted and could conceal an
indirect feature dependency, so these checks do not replace design review.
Project checks see the items evaluated for the current build/configuration, not
inactive configurations or references introduced later by custom execution targets.
They do not check `PackageReference` declarations or the entire transitive closure
of a third-party package. Compiler platform checks and runtime/component/integration
tests still matter.

Future policies, such as prohibiting persistence entities in HTTP payloads or
restricting contract types to framework-independent DTOs/interfaces, require a
separate design decision. Keep exceptions narrow and justified, and update the
documented boundary alongside its checks when intentionally changing the design.

See the [ArchUnitNET guide](https://archunitnet.readthedocs.io/en/latest/guide/)
for namespace, type, member, cycle, and diagram-based rules.

### Choosing an integration layer

Use **Testcontainers** when the subject is server code plus one real dependency:
PostgreSQL SQL translation, migrations, constraints, transactions, Identity stores,
or a service whose dependency injection you need to control. The initial test
applies real migrations, writes an Identity user, reads it in a fresh scope, and
checks normalized duplicate-name handling. Each test owns a disposable container
with dynamic ports, using `postgres:18.3` to match Aspire 13.6. Keep the resource
reaper enabled; do not use development connection strings or container reuse.
For future in-process HTTP tests, `WebApplicationFactory` can be combined with
these containers. This project does not verify AppHost wiring or browser behavior.

Use **Aspire integration tests** when the subject is the assembled application:
resource references, connection injection, migrations before web startup, readiness,
service discovery and HTTP contracts across process boundaries. These use
`Aspire.Hosting.Testing` 13.6.4, matching the AppHost rather than independently
upgrading its testing package. The initial test waits for the real web health
check and migration completion, then checks health, liveness, Home, Login, and
the Development OpenAPI document.
Aspire starts actual processes, so test DI does not replace services in the web
process. Configure resources before `BuildAsync`; retain startup dependencies.

Use **Playwright** for browser-observable behavior: responsive layout and
navigation, focus, forms, cookies and render-mode transitions.
The initial tests run Home → Counter → Home at desktop and mobile widths, increment
the counter, check for JavaScript errors/overflow, and verify `performance.timeOrigin`
is unchanged. This demonstrates document
continuity and working Auto interaction; it does not establish which Auto renderer
was selected, passkey ceremonies, or every account workflow.

Both Aspire and Playwright tests use `Ruvents.Testing.TestAppHost`, a small shared
library that removes container volume/bind mounts and enforces session lifetimes
on the real AppHost model. Aspire's testing builder disables the dashboard and
randomizes proxied ports by default. Each test disposes its builder and application,
including on failure; optional pgAdmin remains unstarted. No manually running
development AppHost or CLI start/stop is required for these test-managed runs.
Readiness uses Aspire notifications with bounded cancellation, not HTTP polling
or fixed sleeps. Use a new browser context for each test; no authentication state
is shared. Only these local HTTPS contexts ignore development certificate errors.

Prerequisites: a running Linux-container Docker engine and available images for
all three projects; Aspire CLI/bundle and the .NET development HTTPS certificate
for Aspire/browser tests; the package-matched Chromium binary for Playwright.
On Linux, install Playwright OS dependencies with
`pwsh ./tests/Ruvents.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium`
in the build agent image/setup. Missing dependencies fail tests rather than
silently skipping them. Unit/bUnit projects still need none of this infrastructure.

Playwright uses `Microsoft.Playwright` directly with our xUnit core MTP runner and
Shouldly. Do not add a runner integration that brings VSTest or xUnit assertions
back into the solution. Locator actions auto-wait; await observable readiness
with locators before asserting values with Shouldly. Do not use `WaitForTimeout`.
Each navigation case attempts to retain `page.png` and `trace.zip` independently
in a unique `bin/<configuration>/net10.0/TestResults/navigation-*` directory.
Artifact capture is best effort: failures are reported through xUnit test output
and do not replace the original test failure or prevent the other capture attempt.
`BrowserArtifactsTests` checks closed-page and unwritable-directory failures;
its disposable outputs use `TestResults/artifact-capture-*`. Inspect the trace
with the generated `playwright.ps1 show-trace <path>` command. These artifacts are
ignored; do not commit traces containing cookies or future test account data.
Keep the existing parallel settings: test processes, ports, containers and browser
contexts must be isolated. On constrained CI agents, bound test-module concurrency
with `--max-parallel-test-modules 1`; this does not change test discovery or skip suites.

References (reviewed October 2, 2026): [Aspire testing overview](https://aspire.dev/testing/overview/),
[AppHost lifecycle and isolation](https://aspire.dev/testing/manage-app-host/),
[Testcontainers PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/),
and [Playwright .NET library](https://playwright.dev/dotnet/docs/library).

### Assertions

Use Shouldly for every assertion in all test projects. The stable version is
centrally pinned to 4.3.0 (verified against NuGet's live version feed on September
26, 2026); the newer 5.0 releases are previews. Import `Shouldly` in test files and
use APIs such as `ShouldBe`, `ShouldBeTrue`, `ShouldBeEmpty`,
`ShouldHaveSingleItem`, and `Should.Throw<T>`. xUnit attributes such as `[Fact]`,
`[Theory]`, and `[InlineData]` continue to define tests.

Do not add the xUnit assertion package, FluentAssertions, or use bUnit assertion
helpers such as `MarkupMatches`. For semantic HTML checks, obtain differences
with `component.CompareTo(expectedMarkup)` and assert `differences.ShouldBeEmpty()`.
This preserves bUnit's semantic comparison instead of comparing raw HTML strings.
Use Shouldly inside `WaitForAssertionAsync` when awaiting a render.

Preserve the strength of existing checks during migrations: single-item checks
must still verify cardinality, and exact exception-type checks use
`Should.Throw<T>(action).ShouldBeOfType<T>()` so derived exceptions do not pass.

### Runner settings

Each project's `testconfig.json` uses xUnit's MTP configuration section:

- `failWarns: true` fails tests that produce xUnit warnings.
- `parallelMode: "all"` permits independent tests, including methods and theory
  rows within the same class, to run in parallel (requires xUnit v3 4.0+).
- `parallelAlgorithm: "conservative"` starts another test when an execution slot
  becomes available, bounding the number of active tests.
- `maxParallelThreads: "1x"` sets that limit to the logical processor count,
  adapting to the developer machine or build agent.
- `preEnumerateTheories: true` discovers each theory data row separately.

MTP automatically copies and renames the file to
`<AssemblyName>.testconfig.json` beside each executable. No manual copy item or
separate `xunit.runner.json` is needed.

### Writing tests for parallel execution

Give each test its own mutable state and resources. Shared fixtures must support
concurrent access. In `all` mode, putting tests in the same class or named
collection does not serialize them. Tests that require exclusive access must
explicitly opt out, for example with `[Fact(DisableParallelism = true)]` or a
`[CollectionDefinition("Exclusive", DisableParallelization = true)]` applied to
the relevant collection. Keep these exceptions narrow and document the shared
resource that requires them.

The default aims to use all available processors for CPU-bound unit tests while
limiting scheduling and memory overhead. If a future suite spends substantial
time awaiting I/O, measure before increasing the multiplier or choosing
`aggressive`; aggressive scheduling changes timing and timeout behavior.

MTP's `--max-parallel-test-modules` controls parallel execution of test modules
separately and defaults to the processor count. With multiple test projects,
balance module concurrency together with each project's xUnit concurrency.

### Writing component tests

Use `BunitContext` and `Render<TComponent>()` (the bUnit 2 APIs). Create a fresh
context inside each test with `await using`; do not share a context through static
state or class/collection fixtures. This keeps the renderer, services, component
state, and JS interop setup isolated while methods and theory rows run in parallel.
Avoid changing bUnit's static defaults from individual tests.

Assert rendered DOM values with Shouldly, or use `CompareTo` followed by
`ShouldBeEmpty` for semantic markup. Dispatch UI events with helpers such as
`ClickAsync`, then use `WaitForAssertionAsync` for Shouldly assertions
that depend on a render; awaiting an event handler alone does not guarantee its
render cycle has finished. When a test must invoke a component's own event
callback instead of a DOM event, reached through
`FindComponents<T>()[0].Instance`, wrap it in `component.InvokeAsync(...)`:
bUnit's renderer rejects state changes made outside its dispatcher.
Register component dependencies in `context.Services`
before rendering and configure required calls on `context.JSInterop` (strict by
default). Keep tests in C# files; any future Razor test helpers must follow the
repository's matching code-behind policy.

bUnit tests run in process without starting Aspire, a web server, or a browser.
They verify component behavior, not browser layout, real JavaScript execution, or
server/WebAssembly render-mode transitions; those need browser-level tests.

Tests rendering shared layout or navigation components need no component-library
services: the UI is plain HTML and CSS, so bUnit and the ASP.NET Core framework
reference supply everything. The shell navigation tests verify native links stay
keyboard reachable without opting out of enhanced navigation. Counter tests
dispatch clicks on the rendered native button and assert the displayed count. They set bUnit's
`RendererInfo` explicitly and check that the button is disabled in static
prerendering and enabled in Server and WebAssembly rendering. Navigation text
assertions trim rendered whitespace while retaining checks for encoded
identity text, conditional account links, and the logout POST/return URL.
bUnit does not validate the CSS responsive layout or the real stylesheets;
check those in a browser through Aspire, including Auto's WebAssembly path.
Navigation checks must cover unchanged `performance.timeOrigin` and no document
requests between Home and Counter, mobile drawer close/reopen across repeated
navigation and browser history, scroll reset/restoration on long pages, and
normal Identity POST/cookie round trips.

## Visual Studio Code

Install and enable the recommended **C# Dev Kit** and **C#** extensions from
`.vscode/extensions.json`. C# Dev Kit 3.40.210 requires VS Code 1.101.0 or newer;
the tested C# extension 2.160.4 requires 1.106.0 or newer. Use a current stable
VS Code and the stable extension channel. Extension recommendations do not pin
versions.

1. Open the repository root and let C# Dev Kit load `Ruvents.slnx`.
2. Build the solution and open the **Testing** view. Allow C# Dev Kit to finish
   loading the solution and discovering the tests; an empty view during startup
   is temporary. The validated stable version discovers tests automatically and
   did not expose a **Test: Refresh Tests** command in this workspace.
3. Expand the project, namespace, class, and theories to see individual cases.
4. Use Test Explorer or editor gutter actions to run or debug tests. Set
   breakpoints in the test and its production code (`IdentityRedirectManager` or
   `Counter.razor.cs`) to step through both.
5. Use **Run Tests with Coverage** to show coverage in the editor.

Keep automatic discovery and build-on-refresh/run enabled (the defaults). Keep
experimental source-only discovery (`dotnet.testWindow.discoverTestsFromSource`)
disabled. No `launch.json` or custom test protocol setting is required. In
particular, do not add the historical
`dotnet.testWindow.useTestingPlatformProtocol`: it is absent from the supported
stable extension's manifest.

## Coverage expansion (September 28, 2026)

That expansion brought the suite to **413 passing cases**: 184 unit tests and 229
component tests, up from 235 cases. The 178 additions cover account management,
authentication revalidation, endpoint responses, shared component contracts,
navigation, and validation boundaries. They also verify failure paths do not
write account data, refresh sessions, send email, or generate credentials
unexpectedly.

The suite has since grown to **438 cases**: 207 unit tests and 231 component
tests. The figures in this section and the acceptance records below describe
those September runs, not the current total.

Validation completed with zero build warnings/errors and zero failed/skipped tests:

```powershell
dotnet build Ruvents.slnx --no-incremental
dotnet test --solution Ruvents.slnx --no-build --report-trx --coverage --coverage-output-format cobertura --results-directory TestResults/final
```

Coverage below merges both projects' Cobertura reports by distinct source filename
and line number, counting a line covered when either project executes it. It
measures `Ruvents`, `Ruvents.UI`, and `Ruvents.ServiceDefaults`; it is line coverage,
not a claim of complete branch or browser coverage. The collector configuration
was not changed to exclude uncovered files.

| Measured scope | Before | After |
| --- | --- | --- |
| Handwritten application code, excluding generated files, migrations, and startup | 918 / 1,480 (62.03%) | 1,449 / 1,480 (97.91%) |
| All instrumented production code, including those categories | 1,100 / 2,954 (37.24%) | 1,794 / 2,954 (60.73%) |

The 31 remaining application lines comprise 20 defensive guard lines whose states
are prevented by initialization or absent forms, three OTLP exporter configuration
lines, and eight trivial lines (no-op email methods, layout/content slots, the
database-context constructor, and the fixed revalidation interval). Tests do not
mutate cached private state or invoke private event handlers merely to cover them.
Generated logging/union code, migrations, startup, build-tool execution, actual
WebAuthn JavaScript, and database persistence remain outside this unit/component
testing scope.

Endpoint tests invoke mapped delegates against in-memory HTTP contexts with
substituted dependencies; they never start their application or send a network
request. Revalidation tests invoke the framework's protected validation hook
directly, avoiding its 30-minute background timer. bUnit tests use actual form
events; the existing form helper supplies posted values only where bUnit does not
run static SSR form mapping. For example, the profile form helper supplies the
nullable posted phone value that static SSR mapping provides.

## Initial test scope

The account outcome suite exercises the real sign-in, passkey, registration,
email-change, and two-factor services with substituted Identity dependencies.
It verifies each outcome, payloads, operation ordering, short-circuited failures,
credential/token decoding, and the passkey limit. Component tests exercise the
real pages and services to check validation, redirects, account-privacy responses,
partial-failure messages, and recovery-code rendering. Every test owns its mutable
state; no database, Aspire process, or browser is required.

The following initial-scope descriptions and acceptance records document the
original test setup before the account outcome suite was added.

The 16 server unit cases cover null/empty/relative destinations, absolute destinations within
the application, rejection of external destinations, query replacement and
encoding, current-page redirects, and status-cookie values and attributes.
Tests use an in-memory recording `NavigationManager` and `DefaultHttpContext`;
they do not start the web server, Aspire, a browser, or a database.

The five component cases exercise the real `Counter`: initial markup, count updates
after one/two/five clicks, and independent state in separate contexts.

## Acceptance record

Verified on September 26, 2026, on Windows x64 with .NET SDK 10.0.401,
VS Code **1.139.1**, C# Dev Kit **3.40.210**, and C# **2.160.4**.
The initial acceptance checks below used `parallelMode: "collections"` and xUnit
assertions; the subsequent parallel configuration and Shouldly migration are
documented separately. Source line references in historical editor checks refer
to the files as they existed during those checks.

| Requirement | Evidence |
| --- | --- |
| Full solution builds | `dotnet build Ruvents.slnx`: 0 warnings, 0 errors. |
| Project execution | `dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj`: 16 passed, 0 failed, 0 skipped. |
| Solution execution and reports | The solution command above passed all 16 cases; TRX counters and Cobertura XML were parsed successfully. |
| Exact dependencies and analyzers | `project.assets.json` resolved the versions above; MSBuild `ResolveReferences` included xUnit and all shared analyzers. Nullable and warnings-as-errors remained enabled. |
| Configuration deployment | Output contained `Ruvents.UnitTests.testconfig.json` with the configured xUnit options. |
| Empty selection fails | The unmatched `--filter-method` command returned nonzero; MTP reported exit code 8. |
| Editor discovery | C# Dev Kit discovered all 16 cases, including facts and individual theory rows. Discovery was automatic; no manual refresh command was exposed. |
| Editor execution | Run All and class execution passed 16/16; an individual fact and the null-input theory row each passed 1/1. The fact was run from its editor gutter. |
| Editor debugging | Debug Test paused at `IdentityRedirectManagerTests.cs:102` and `IdentityRedirectManager.cs:29`, then completed. Temporary breakpoints were removed. |
| Build on run and failure display | Temporarily changing the current-page assertion produced 15/16 passing and an `Assert.Equal` failure without a manual build. The assertion was restored. |
| Editor coverage | Run Tests with Coverage rebuilt the restored source, passed 16/16, populated Test Coverage, and displayed covered-line decorations in the redirect manager. |
| Reload | After Developer: Reload Window and solution initialization, discovery returned automatically and Run All passed 16/16 again. |

After enabling `all` / `conservative` / `1x`, `dotnet build Ruvents.slnx` passed
with 0 warnings and 0 errors. Running
`dotnet test --project tests/Ruvents.UnitTests/Ruvents.UnitTests.csproj --no-build`
passed all 16 tests (0 failed, 0 skipped). The deployed
`Ruvents.UnitTests.testconfig.json` contained the updated parallel settings.

The source assertions were reviewed against the requested behavior matrix:

| Requirement | Evidence |
| --- | --- |
| Null, empty, and relative destinations | `RedirectToRelativeDestinationNavigatesOnce` (4 rows). |
| Absolute destinations inside the application | `RedirectToAbsoluteDestinationWithinApplicationConvertsToRelativePath` (3 rows). |
| Rejected external absolute destinations | `RedirectToAbsoluteDestinationOutsideApplicationThrowsWithoutNavigating` (4 rows). |
| Query replacement and encoding | `RedirectToQueryParametersReplacesExistingQueryAndEncodesValues`. |
| Empty replacement query | `RedirectToEmptyQueryParametersRemovesExistingQueryAndFragment`. |
| Current-page redirect | `RedirectToCurrentPageRemovesQueryAndFragment`. |
| Status-cookie contents and attributes | `RedirectToWithStatusWritesStatusCookieAndNavigates` and `RedirectToCurrentPageWithStatusWritesStatusCookieAndRemovesQueryAndFragment`. |

### Component project acceptance

The bUnit addition was verified on September 26, 2026 with the same SDK and editor
versions above, using `all` / `conservative` / `1x` in both projects.

| Requirement | Evidence |
| --- | --- |
| Stable bUnit at that time | NuGet's live version feed and the published release identified 2.11.3; the restored assets resolved that exact version. The pinned version is now 2.13.3. |
| Full build | `dotnet build Ruvents.slnx --no-incremental`: 0 warnings, 0 errors. |
| Direct component execution | `dotnet test --project tests/Ruvents.ComponentTests/Ruvents.ComponentTests.csproj --no-build`: 5 passed, 0 failed, 0 skipped. |
| Solution execution and reports | The solution coverage command passed 21/21; separate TRX and Cobertura reports were generated for both projects. The component TRX contains five passing results, and Cobertura includes `Counter.razor` and `Counter.razor.cs`. |
| Inherited checks and deployed settings | MSBuild reports `IsTestProject=true`, nullable enabled, warnings-as-errors enabled, and xUnit/Meziantou/Sonar/Roslynator analyzers. The output contains `Ruvents.ComponentTests.testconfig.json` with the parallel settings above. |
| Empty selection fails | The component project's unmatched `--filter-method NoSuchTestMustNotExist` returned nonzero; MTP reported exit code 8. |
| Editor discovery and execution | C# Dev Kit automatically discovered both projects, facts, and all three component theory rows. Run All passed 21/21; running only the two-click theory row passed 1/1. |
| Editor debugging | Debug Test for `IndependentContextsKeepCounterStateIsolatedAsync` hit breakpoints at `CounterTests.cs:52` and `Counter.razor.cs:9`, then passed 1/1. Both temporary breakpoints were removed. |
| Editor gutter execution | Run Test from the gutter beside `IndependentContextsKeepCounterStateIsolatedAsync` passed 1/1. |
| Editor coverage | Run Tests with Coverage passed 21/21, populated Test Coverage, and displayed covered-line decorations on `Counter.razor.cs`. |

| Component behavior | Exact test evidence |
| --- | --- |
| Initial markup, status, and button | `CounterTests.RenderShowsInitialCountAndIncrementButtonAsync` uses semantic markup comparison. |
| One, two, and five clicks update the displayed status | `CounterTests.ClickingIncrementButtonUpdatesDisplayedCountAsync` (three theory rows) dispatches real Blazor click events and awaits the rendered assertion. |
| Component state is isolated | `CounterTests.IndependentContextsKeepCounterStateIsolatedAsync` verifies clicking in one context leaves the other at zero. |

### Shouldly migration acceptance

Verified on September 26, 2026: `dotnet build Ruvents.slnx` completed with
0 warnings and 0 errors, and `dotnet test --solution Ruvents.slnx --no-build`
completed with 21 passed, 0 failed, and 0 skipped. Both projects resolve Shouldly
4.3.0 and `xunit.v3.core.mtp-v2` 4.0.1, retain xUnit analyzers 2.1.0, and no longer
resolve `xunit.v3.assert`. All existing assertion calls were migrated to Shouldly,
including the component's semantic markup difference check. The parallel and MTP
runner settings are unchanged.

## References

- [Shouldly 4.3.0](https://www.nuget.org/packages/Shouldly/4.3.0)
- [Shouldly assertion documentation](https://docs.shouldly.org/)
- [xUnit framework and assertion package separation](https://xunit.net/docs/nuget-packages-v3)
- [bUnit project setup](https://bunit.dev/docs/getting-started/create-test-project.html)
- [bUnit 2.13.3 release](https://github.com/bUnit-dev/bUnit/releases/tag/v2.13.3)
- [bUnit context and rendering](https://bunit.dev/api/Bunit.BunitContext.html)
- [bUnit event dispatch and asynchronous rendering](https://bunit.dev/docs/interaction/trigger-event-handlers.html)
- [xUnit MTP setup](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [xUnit testconfig.json](https://xunit.net/docs/config-testconfig-json)
- [xUnit parallel execution and opt-outs](https://xunit.net/docs/running-tests-in-parallel)
- [.NET 10 dotnet test integration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
- [MTP test-module concurrency](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp#options)
- [Microsoft code coverage extension](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-extensions-code-coverage)
- [VS Code C# testing](https://code.visualstudio.com/docs/csharp/testing)
- [C# Dev Kit 3.40.210 manifest](https://ms-dotnettools.gallery.vsassets.io/_apis/public/gallery/publisher/ms-dotnettools/extension/csdevkit/3.40.210/assetbyname/Microsoft.VisualStudio.Code.Manifest)
