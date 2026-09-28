# Ruvents

.NET 10 Blazor application with a hosted WebAssembly client, shared UI and kernel,
Aspire orchestration, and PostgreSQL-backed ASP.NET Core Identity.

## Local prerequisites

- .NET SDK **10.0.401** (selected by `global.json`).
- Aspire CLI **13.5.4** on `PATH`, matching the AppHost SDK and stable integrations.
  Follow the [Aspire installation guide](https://aspire.dev/get-started/install/).
- Docker Desktop running Linux containers, or another Aspire-supported container runtime.
- A trusted .NET development HTTPS certificate: `dotnet dev-certs https --trust`.
- Optional VS Code with this repository's recommended C# and Aspire extensions.
  The editor opens `Ruvents.slnx` by default.

Run `dotnet --version`, `aspire --version`, and `aspire doctor` from the repository
root to verify the environment. The AppHost is explicitly located by the root
`aspire.config.json`.

## Run through Aspire

Aspire is the default entry point for running and debugging the application. It
starts PostgreSQL, applies migrations, supplies configuration, and starts the web
project. The hosted WebAssembly client runs through that web project.

For interactive development, run from the repository root:

```powershell
aspire run
```

The CLI builds the AppHost and its projects before starting them. For a background
run (including agent validation), use:

```powershell
aspire start --non-interactive
aspire wait ruvents --timeout 120 --non-interactive
aspire describe --non-interactive
```

`aspire start` prints the dashboard login URL. The dashboard and `aspire describe`
show the current application endpoints. Open the HTTPS endpoint for
`ruvents.dev.localhost`; all ports are allocated dynamically and can change on
restart. Chromium browsers resolve `.localhost` names locally. Command-line
clients that do not resolve subdomains can use the internal `https://localhost`
endpoint shown by Aspire. Do not copy ports or dashboard login tokens into source.

The AppHost defaults to its first launch profile, `https`. `AddProject` selects the
matching web profile and derives its endpoints from `applicationUrl`; there is no
AppHost override for the web profile or its ports. The web profiles retain
`ruvents.dev.localhost:0`: the hostname is our local convention, and port `0` asks
Aspire to allocate an available port instead of using the template's fixed ports.
The other web launch settings preserve the template's environment and Blazor debugging.

The AppHost's `https` profile omits dashboard, OTLP, and resource-service addresses:
Aspire 13.5.4 supplies secure, dynamically allocated local endpoints. The optional
AppHost `http` profile sets `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, which opts into
HTTP endpoints with allocated ports. This opt-in applies only when that profile
is selected; the default `https` profile keeps Aspire's secure defaults.

Use the web resource's **Rebuild** command to apply compiled application changes.
For AppHost changes or a full solution build, stop first to release Windows file locks:

```powershell
aspire stop --non-interactive
dotnet build Ruvents.slnx
aspire start --non-interactive
```

Stop the application with `aspire stop --non-interactive` (or Ctrl+C for a foreground
run). PostgreSQL and any started pgAdmin container stop with Aspire. The database
volume survives. Aspire retains its generated local PostgreSQL password in AppHost
user secrets; preserve those secrets along with the volume when reusing local data.
No database password or connection string needs to be committed.

## Debug through Aspire

In VS Code, install the recommended Aspire and C# extensions and open the repository
root. Select **Aspire: Ruvents** in Run and Debug, then press **F5**. The checked-in
`.vscode/launch.json` uses the Aspire debugger and discovers the AppHost through
`aspire.config.json`; no fixed ports or connection strings are needed. The extension
starts the resource graph and attaches the .NET debugger to the web project.
Use **Ctrl+F5** with the same configuration to run without debugging.

Stop any CLI-started instance with `aspire stop --non-interactive` before starting
an IDE debug session. Use the IDE's **Stop Debugging** action to end its session.
Open the dashboard through **Aspire: Open Dashboard** or the Aspire view; editor
settings control whether it opens automatically. This workspace sets
`aspire.dashboardBrowser` to `openExternalBrowser`, so the dashboard opens in the
configured external browser for both F5 and Ctrl+F5. This overrides a user-level
`debugEdge`/`debugChrome` preference; C# application debugging remains enabled with F5.

If VS Code reports **Unable to launch browser: "Unable to attach to browser"**,
check **Output → Aspire Extension**. An entry such as **Failed to start debug
browser (pwa-msedge), falling back to default browser** identifies the dashboard's
browser-debugger session. That session is separate from the AppHost and web C#
debuggers and is not needed to use the dashboard. Keep the workspace's
`openExternalBrowser` setting and remove any `dashboardBrowser: "debugEdge"` or
`"debugChrome"` override in the selected `launch.json` configuration, which takes
precedence over workspace settings. Then stop and restart the Aspire session.

Set server breakpoints in the web project's code-behind, such as the registration
handler in `src/Ruvents/Features/Account/Pages/Register.razor.cs`, then exercise the
page through the HTTPS endpoint shown by Aspire. Inspect variables and the call
stack in the debugger, and use Aspire's logs and traces for the corresponding
HTTP and database activity. A breakpoint can temporarily interrupt health checks;
resume execution before diagnosing the paused resource as unhealthy.

In Visual Studio, set **Ruvents.AppHost** as the startup project, select its
**https** profile, and use F5 (debug) or Ctrl+F5 (run). Launching `Ruvents` or
`Ruvents.Client` alone does not start the database/migration dependency graph.

The existing unit and component tests remain headless and can run/debug through
Test Explorer without Aspire.

See the [Aspire VS Code extension guide](https://aspire.dev/get-started/aspire-vscode-extension/).

## Styling

The UI uses standard CSS with Grid as the default for structured layout and
alignment. The shell has always-visible top navigation; account pages stack
their sections and use forms capped at 36rem. The baseline is intentionally
plain: a system font, neutral light colors, native controls, and visible focus.

Shared styles live in `src/Ruvents/wwwroot/app.css`: sizing, typography, forms
(`account-form`, `form-field`, `checkbox-field`), action groups (`actions`),
notices (`notice` with a semantic `data-kind`), and table overflow
(`table-container`). Component-specific styles belong in adjacent `.razor.css`
files. Use narrowly scoped `::deep` selectors for child component markup.

Use normal flow for prose and semantic tables, and positioning for overlays.
Flexbox needs a specific benefit; do not recreate Bootstrap utilities or add
inline layout styles, `!important`, decorative icons, or animations. Keep form
labels before their controls and preserve Blazor/Identity behavior hooks when
editing markup. Check narrow screens, keyboard focus, and text wrapping when
changing layouts. See `AGENTS.md` for the authoring conventions.

## Resource graph and database

```text
postgres (PostgreSQL 18.3, managed data volume)
  ├─ ruventsdb (physical database: ruvents)
  │    └─ ruvents-migrations (EF database update)
  │         └─ ruvents (Blazor server + hosted WebAssembly)
  └─ pgadmin (explicit start)
```

After a normal startup, these dashboard states are expected:

| Resource | Expected state | Meaning |
| --- | --- | --- |
| `postgres`, `ruventsdb` | Running / Healthy | PostgreSQL and the application database are ready. |
| `ruvents-migrations` | Finished | The one-shot migration command completed successfully. It is not a long-running service. |
| `ruvents` | Running / Healthy | The web application is ready and its database readiness check passes. |
| `pgadmin` | Not started | Optional database UI; start it when needed. |

With hidden resources displayed, `ruvents-rebuilder` can also be **Not started**
until the web project's Rebuild command is used, and the EF tool resource can be
**Finished**. These helper states do not indicate an application startup failure.
An **Exited**, **Failed**, or **Unhealthy** application/database resource, or web
startup blocked by failed migrations, does require investigation through its logs.

The PostgreSQL image version is the built-in default of
`Aspire.Hosting.PostgreSQL` 13.5.4. Container lifetime remains Aspire's default
session lifetime. Data uses an Aspire-managed named volume rather than a repository
file. This is a fresh PostgreSQL schema; the original SQLite scaffold is removed.

Aspire supplies `ConnectionStrings:ruventsdb` to both the application and migration
tool. The application uses Npgsql EF Core 10.0.3, EF Core 10.0.12, and Aspire's
Npgsql EF integration 13.5.4. Its non-pooled `IDbContextFactory<ApplicationDbContext>`
supports a context per Blazor operation; Identity can still resolve the scoped
context. Dispose factory-created contexts with `await using`.

Identity schema version 3 is retained, including the `AspNetUserPasskeys` table.
Development registration uses the existing no-op email sender: follow the confirmation
link on the registration confirmation page before logging in. This setup does not
configure a production email service or deployment infrastructure.

## EF migrations

`Aspire.Hosting.EntityFrameworkCore` **13.5.4-preview.1.26464.4** manages
`ruvents-migrations` and its **dotnet-ef 10.0.12** tool. It does not alter the machine's
global EF tool. On every start, migrations wait for PostgreSQL and the database;
the web application starts only after migration success. Failure blocks web startup.
Migrations use the actual web startup registration, keeping Identity's design-time
and runtime models aligned.

For a schema change:

1. Stop Aspire, change the model, and build the solution.
2. Start Aspire. If EF detects pending model changes, migration startup can fail and
   block the web resource; the migration authoring commands remain available.
3. On `ruvents-migrations` in the dashboard, run **Add Migration...** and supply a
   descriptive name. Review the generated files in `src/Ruvents/Data/Migrations`
   with namespace `Ruvents.Migrations`.
4. Apply the repository's conventions to the handwritten migration class (file-scoped
   namespace and `internal sealed partial class`); leave generated designer code alone.
   Keep the model snapshot in the same migrations directory. EF can choose a directory
   from the legacy namespace when regenerating a missing snapshot, so check its location.
5. Stop Aspire, rebuild, and start again. The built-in migration resource applies the
   newly compiled migration before starting the web application.
6. Run **Get Database Status** on `ruvents-migrations` to verify applied migrations and
   pending model changes.

CLI equivalents for inspecting and updating the current compiled model:

```powershell
aspire resource ruvents-migrations ef-database-status --non-interactive
aspire resource ruvents-migrations ef-database-update --non-interactive
aspire logs ruvents-migrations --non-interactive
```

The built-in **Remove Migration**, **Drop Database**, and **Reset Database** commands
are also available. Drop/reset delete local application data; use them only when
that is intended. There is no application-startup migration routine or custom worker.
See [Aspire's EF migration integration](https://aspire.dev/integrations/databases/efcore/migrations/).

## Health, telemetry, and pgAdmin

The application references `Ruvents.ServiceDefaults`. In Development, `/health`
checks readiness including PostgreSQL connectivity; `/alive` checks process liveness
independently of PostgreSQL. Aspire monitors `/health`. The database readiness check
has a five-second timeout so EF's transient retries do not hold an unhealthy response
open for minutes. Cancellation is cooperative: an in-flight Npgsql connection attempt
can delay the response (about 15 seconds in local outage validation). Normal
application operations retain Aspire's retry behavior.

The dashboard exposes server logs, request traces, Npgsql database spans, and metrics.
Useful CLI commands include `aspire logs ruvents`, `aspire otel traces`, and
`aspire otel logs`. The Aspire MCP server provides the same resource and telemetry
visibility to agents. Database query telemetry can contain application information;
keep exports and runtime logs out of Git.

pgAdmin is available only in run mode. Start `pgadmin` from its dashboard action or:

```powershell
aspire resource pgadmin start --non-interactive
aspire wait pgadmin --timeout 120 --non-interactive
```

Open its generated HTTP endpoint, expand **Servers → postgres → Databases → ruvents**.
Aspire configures the connection and credentials. Stop it from the dashboard when done.

## Agent tools and skills

Shared repository skills live only in `.agents/skills`, supported by Codex and
Copilot CLI. Copilot desktop inherits repository/CLI skills and MCP configuration.
The Aspire skills were reconciled with the first-party **aspire-skills v0.0.1**
bundle referenced by CLI 13.5.4 (including its published SHA-512). Its descriptions
still refer to Aspire 13.4; those upstream headings are intentionally unchanged.
Use installed package/API evidence and current documentation when versions differ.

The existing user-level Codex and Copilot MCP entries run **`aspire agent mcp`**.
Keep a single entry per agent. No repository MCP duplicate or VS Code agent
configuration is required. On a new machine, install Aspire on `PATH`, then:

- Codex: `codex mcp add aspire -- aspire agent mcp` (unless already configured).
- Copilot CLI: use `/mcp add` to add a local/stdio server named `aspire`, command
  `aspire`, arguments `agent mcp`, at user scope. Its configuration is stored in
  `~/.copilot/mcp-config.json` and is inherited by Copilot desktop.
- Restart/reload the agent after configuration. Start the application from this
  repository and use the Aspire MCP resource-list tool to verify the connection.
  If several AppHosts are running, select this repository's AppHost explicitly.

The checked-in skills require no additional installation on clone. To refresh them,
use the matching CLI's `aspire agent init` with the **standard** skill location
(`.agents/skills`), then review the diff. Avoid creating alternate copies under
`.github`, `.codex`, or VS Code agent configuration. Keep user secrets, dashboard
tokens, runtime `.aspire` state, telemetry exports, and temporary browser output out
of source control.

References: [Aspire MCP](https://aspire.dev/reference/cli/commands/aspire-agent-mcp/),
[Codex skills](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills),
[Copilot CLI skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills),
[Copilot desktop](https://docs.github.com/en/copilot/how-tos/github-copilot-app/customize-github-copilot-app).

## Validation

The existing tests run headlessly with native Microsoft.Testing.Platform and
Shouldly; they do not require Aspire or PostgreSQL:

```powershell
dotnet build Ruvents.slnx
dotnet test --solution Ruvents.slnx
```

See [tests/README.md](tests/README.md) for test conventions and
[build/README.md](build/README.md) for Razor code-behind validation.

### Local Aspire verification

Verified on September 27, 2026 with the pinned SDK/packages and VS Code Aspire
extension 1.23.0:

| Check | Result |
| --- | --- |
| Environment | `aspire doctor`: 8 passed, 0 warnings, 0 failed. |
| CLI and IDE startup | PostgreSQL/database healthy; migrations finished; HTTPS web resource healthy in both runs. |
| VS Code debugging | **Aspire: Ruvents** launched the AppHost and web C# debug sessions. A breakpoint in `RegisterUserAsync` stopped at the Identity create call; variable inspection, stepping over the awaited database operation, and continue succeeded. |
| VS Code dashboard browser | Reproduced the `debugEdge` browser-attach failure while the web resource was healthy. With workspace `openExternalBrowser`, F5 and Ctrl+F5 opened the dashboard without the dialog; the web resource was healthy, and logs confirmed C# debugging enabled only for F5. |
| Identity / PostgreSQL | Disposable registration, scaffold confirmation, login/logout, profile update, and passkey-list query succeeded. Account and profile survived a full Aspire restart; the disposable account was deleted afterward. |
| Migrations | 1 applied, 0 pending; no pending model changes. |
| Readiness / liveness | Both returned 200 while healthy. With PostgreSQL stopped, `/health` returned 503 and `/alive` remained 200. Readiness recovered to 200 after database restart. |
| Developer tools | pgAdmin connected to `ruvents`; dashboard/MCP exposed HTTP and PostgreSQL traces and dashboard metrics. |
| Build / tests | `dotnet build Ruvents.slnx`: 0 warnings/errors. `dotnet test --solution Ruvents.slnx`: 21 passed, 0 failed, 0 skipped. |

Temporary breakpoints, test browsers, and Aspire verification runs were closed.
These are local acceptance checks, not a new integration-test suite. IDE breakpoint
verification covered the server-side registration handler; it did not verify
WebAssembly client breakpoints or Visual Studio debugging.
