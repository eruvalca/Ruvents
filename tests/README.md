# Unit and component tests

Both test projects target .NET 10 and use xUnit with native
Microsoft.Testing.Platform (MTP) integration. Open the repository root so both
the CLI and editor find `global.json` and `Ruvents.slnx`.

| Project | Scope |
| --- | --- |
| `Ruvents.UnitTests` | Account services and extensions, outcome decoding, `IdentityRedirectManager`, Identity routes, and service defaults. |
| `Ruvents.ComponentTests` | Account page rendering and interactions using bUnit, account test helpers, and the shared UI's `Counter`. |

## Supported stack

Stable versions selected on September 26, 2026:

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.401 |
| bunit (component project only) | 2.11.3 |
| xunit.v3.core.mtp-v2 | 4.0.1 |
| Shouldly | 4.3.0 |
| NSubstitute (account tests) | 6.2.0 |
| Microsoft.Testing.Platform / Platform.MSBuild | 2.4.1 |
| Microsoft.Testing.Extensions.TrxReport | 2.4.1 |
| Microsoft.Testing.Extensions.CodeCoverage | 18.11.2 |
| xunit.analyzers | 2.1.0 |
| C# Dev Kit (stable baseline) | 3.40.210 |

Package versions are centralized in `Directory.Packages.props`. Explicit MTP
runtime and MSBuild references advance xUnit's transitive 2.4.0 dependencies to
2.4.1. Test package references use `PrivateAssets="all"`. Both test projects
reference xUnit's core MTP package, which supplies the framework and runner
without `xunit.v3.assert`; xUnit analyzers remain included by the root build props.

The root build props recognize project names ending in `.UnitTests` or `.ComponentTests` before
evaluating shared analyzer references. Tests inherit nullable analysis, code style
rules, and warnings-as-errors. xUnit test classes are public and sealed; their
type-level CA1515 suppression documents the discovery requirement. The server
grants `Ruvents.UnitTests` and `Ruvents.ComponentTests` access to its internal types.
The component project uses the Razor SDK and references both `Ruvents.UI` and the
server project. The server also grants `DynamicProxyGenAssembly2` internal access
so NSubstitute can proxy Identity dependencies closed over the internal
`ApplicationUser` type.

`global.json` selects native .NET 10 MTP mode. Do not add VSTest packages
(`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`) or the legacy
`TestingPlatformDotnetTestSupport` bridge to these projects. bUnit provides the
component renderer and comparison tools; xUnit and MTP provide discovery and
execution, and Shouldly is the exclusive assertion library.

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

### Assertions

Use Shouldly for every assertion in both test projects. The stable version is
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
budget module concurrency together with each project's xUnit concurrency.

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
render cycle has finished. Register component dependencies in `context.Services`
before rendering and configure required calls on `context.JSInterop` (strict by
default). Keep tests in C# files; any future Razor test helpers must follow the
repository's matching code-behind policy.

bUnit tests run in process without starting Aspire, a web server, or a browser.
They verify component behavior, not browser layout, real JavaScript execution, or
server/WebAssembly render-mode transitions; those need browser-level tests.

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

## Initial test scope

The account outcome suite exercises the real sign-in, passkey, registration,
email-change, and two-factor services with substituted Identity dependencies.
It verifies each outcome, payloads, operation ordering, short-circuited failures,
credential/token decoding, and the passkey limit. Component tests exercise the
real pages and services to check validation, redirects, account-privacy responses,
partial-failure messages, and recovery-code rendering. Every test owns its mutable
state; no database, Aspire process, browser, or external provider is required.

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
| Current stable bUnit | NuGet's live version feed and the published release identify 2.11.3; the restored assets resolve that exact version. |
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
- [bUnit 2.11.3 release](https://github.com/bUnit-dev/bUnit/releases/tag/v2.11.3)
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
