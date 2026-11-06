# Ruvents

.NET 10 Blazor application with a hosted WebAssembly client, shared UI and kernel,
Aspire orchestration, and PostgreSQL-backed ASP.NET Core Identity.

## Local prerequisites

- .NET SDK **10.0.401** (selected by `global.json`).
- Aspire CLI **13.6.4** on `PATH` (the version the workflows pin), with
  the AppHost SDK on **13.6.4**.
  Follow the [Aspire installation guide](https://aspire.dev/get-started/install/).
- Docker Desktop running Linux containers, or another Aspire-supported container runtime.
- A trusted .NET development HTTPS certificate: `dotnet dev-certs https --trust`.
- Optional VS Code with this repository's recommended C# and Aspire extensions.
  The editor opens `Ruvents.slnx` by default.

Run `dotnet --version`, `aspire --version`, and `aspire doctor` from the repository
root to verify the environment. The AppHost is explicitly located by the root
`aspire.config.json`.

## First run and application identity

Use PowerShell 7 for the scripts in this repository. From the solution root,
run `dotnet build Ruvents.slnx`, then `aspire run`. The first build/start may
restore NuGet packages, download Aspire/EF tooling, and pull container images.
The initial migration creates an empty Identity schema; no accounts or local
credentials are included. Git initialization is optional and separate.

If this solution was generated, `.template-provenance.json` records its template
version and source commit. It is an independent snapshot: template updates do not
update this application. Review SDK/package/skill updates in this repository.

Use a unique application name for projects you run side by side. Different ports
do not isolate browser cookies on the same hostname. A generated secrets ID is
unique even when the application name is reused, but the development hostname is
name-based. Aspire's default data-volume name depends on the AppHost path; moving
or renaming a checkout can select a different volume. Keep the original secrets
and volume together if preserving development data.

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
aspire start --launch-profile https --non-interactive
aspire wait ruvents-web --timeout 120 --non-interactive
aspire describe --non-interactive
```

`aspire start` prints the dashboard login URL. The dashboard and `aspire describe`
show the current application endpoints. Open the HTTPS endpoint for
`ruvents.dev.localhost`; all ports are allocated dynamically and can change on
restart. Chromium browsers resolve `.localhost` names locally. Command-line
clients that do not resolve subdomains can use the internal `https://localhost`
endpoint shown by Aspire. Do not copy ports or dashboard login tokens into source.

The background command explicitly selects `https`; it is also the AppHost's first
and default launch profile. `AddProject` selects the
matching web profile and derives its endpoints from `applicationUrl`; there is no
AppHost override for the web profile or its ports. The web profiles retain
`ruvents.dev.localhost:0`: the hostname is our local convention, and port `0` asks
Aspire to allocate an available port instead of using the template's fixed ports.
The other web launch settings preserve the template's environment and Blazor debugging.

The AppHost's `https` profile omits dashboard, OTLP, and resource-service addresses:
Aspire 13.6.4 supplies secure, dynamically allocated local endpoints. The optional
AppHost `http` profile sets `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, which opts into
HTTP endpoints with allocated ports. This opt-in applies only when that profile
is selected; the default `https` profile keeps Aspire's secure defaults.

Use the web resource's **Rebuild** command to apply compiled application changes.
For AppHost changes or a full solution build, stop first to release Windows file locks:

```powershell
aspire stop --non-interactive
dotnet build Ruvents.slnx
aspire start --launch-profile https --non-interactive
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

## Planning features

The application plans family gatherings:

- **Family groups** are discoverable by name. A public directory lets anyone find a
  group, and an owner or organizer approves each join request before it grants access.
- **Household members** belong to the person's account, so children and other people
  without an account can be marked attending on an RSVP without invented logins.
- **Events** carry a title, description, structured location, map link, start/end,
  all-day flag, time zone, optional RSVP deadline, and an optional cover image. Cover
  images are only served to members of the event's family group, and only an accepted
  image type is ever returned. Any
  group member may create an event; the creator or any organizer can edit, cancel, or
  restore it. The end time defaults to 30 minutes after the start and must stay after
  it. Creating an event marks the creator as attending, and they can still adjust that
  RSVP, including which of their household members attend. A member of exactly one
  family group gets that group selected automatically when creating an event.
- **RSVPs** are per person: the account holder, their household members, and named
  additional guests each count separately. A soft deadline closes responses, and the
  creator or an organizer can reopen it.
- **Potluck sign-up** is hybrid: organizers pre-fill slots with a needed quantity, and
  members can also add their own item. Claims record a quantity and an optional
  description, and organizers can rename or resize a slot, assign one to a member, or
  remove any claim. A slot is locked for the duration of each change, so simultaneous
  claims queue instead of over-claiming or failing.
- **Announcements and comments** form a two-level thread — top-level posts (comments or
  organizer announcements) plus one level of replies.
- **In-app notifications** cover join requests, event changes, announcements, and
  potluck changes, with unread state.
- **Administration** gives a platform `Admin` role password resets, account suspension,
  and family-group removal. Deleting your own account clears the planning data it owns
  (memberships, household members, RSVPs with their guests, potluck claims, and
  notifications) while the groups and events it contributed to stay with the family, so
  a deleted account cannot leave claims blocking a potluck slot. The cleanup and the
  account deletion commit together, and a group whose last organizer leaves promotes its
  longest-standing member, so the family can still approve join requests. A suspended
  account cannot sign in on any path, because the
  suspension check rides the confirmation hook that `SignInManager` consults for
  password, passkey, external, and two-factor sign-in.
  The account matching the configured `Platform:AdminEmail` is promoted automatically at
  registration or sign-in, but only after the operator has confirmed that account's email:
  registration cannot prove ownership of an address, so a matching address alone must not
  grant administrator privileges.

## Layout, styling, and interaction

`src/Ruvents.UI/wwwroot/theme.css` owns the visual identity: design tokens for colour,
type, spacing, elevation, and radius, plus the shared element and card styling. It
supports light and dark schemes through `prefers-color-scheme`, respects
`prefers-reduced-motion`, and keeps focus outlines visible. Pages compose design-system
classes (`notice`, `chip`, `card-grid`, `event-title`, `event-when`, `event-where`)
with a scoped `.razor.css` file for local layout. There is still no component library,
CSS framework, or CSS preprocessor.

The shell offers an explicit colour-mode control — auto (follows the system), light,
or dark. The choice is stored in `localStorage` and applied by `theme.js`; a small
inline script applies it before first paint so the wrong scheme never flashes.
`theme.css` defines its dark tokens twice, once for `[data-theme="dark"]` and once for
the system setting, so keep the two blocks in sync. Its base block forces
`[hidden] { display: none !important; }` because an author `display` rule on a class
such as `.notice` would otherwise beat the user agent's `[hidden]` styling.

Profile photos are cropped before upload. `PhotoCropField` wraps Cropper.Blazor and
frames the crop box as a circle, so the user sees the avatar they are producing
rather than a square. The cropped image travels
to the server in a hidden `PhotoData` field, so registration keeps its normal static
form post; without JavaScript the `<noscript>` file input still supplies the photo.
`ProfilePhotoWriter` owns the shared replace path, which the required-photo gate at
`/Account/Photo` and `Account/Manage/Photo` both use; account management also shows
the current photo on the profile page. The picker stays disabled until the circuit
connects, because a selection made during prerendering is dropped without notice.
The component renders the form's submit button only once a crop exists, because a
static page cannot observe state inside an interactive child.

Planning pages live in `Ruvents.UI` so they can run under either renderer. They depend
on `IPlanningApi`: the server host implements it with the application services and the
WebAssembly host calls the `/api/planning` JSON endpoints. Account and Identity pages
stay in the server project because they need static form posts, cookies, and
`HttpContext`. The shared kernel holds the DTOs both hosts serialize.

Markup uses native elements — `<button>`, `<a>`, `<ul>`, `<section>` — and Blazor's
own components: `NavLink`, `AuthorizeView`, `PageTitle`, `AntiforgeryToken`,
`EditForm` and the `Input*` family, `ValidationMessage`, `Router`,
`AuthorizeRouteView`, and `FocusOnNavigate`. Do not wrap a native element in a
custom component that only forwards attributes. A `<button type="button">` with
`@onclick` is the stock way to handle a click; Blazor ships no button component.

CSS grids carry the structure:

- `src/Ruvents.UI/Layout/MainLayout.razor.css` defines one grid with named `header`,
  `navigation`, `content`, and `footer` areas. At 52rem and wider the navigation is a
  sticky column beside the content; below that the same grid collapses to a single
  column with a horizontally scrolling navigation above the content. The navigation is
  always rendered, so no toggle, overlay, focus trap, or JavaScript is involved and the
  shell works under static SSR.
- `src/Ruvents/Features/Account/Components/ManageLayout.razor.css` places the account
  menu beside the page content once there is room, and stacks them below that.
- `src/Ruvents.UI/Features/Home/Pages/Home.razor.css` builds the aurora hero and an
  `auto-fit` feature grid. Track minimums are clamped with `min(100%, 16rem)` so
  enlarged browser text cannot force overflow.

`src/Ruvents/wwwroot/app.css` holds functional rules only: form controls stay at
16px so mobile browsers do not zoom on focus, and `.table-container` keeps wide
tables inside the content column instead of widening the page. Presentational rules
belong in `theme.css` or in a scoped `.razor.css` file for the component that needs
them.

Navigation uses ordinary `NavLink` anchors inside `<ul>` lists. Native anchors
preserve accessible names and keyboard order before JavaScript initializes, and
`NavLink` marks the current page with `aria-current="page"`. Give each navigation
region its own `<nav aria-label>` element: `NavMenu` and `ManageNavMenu` each own
one, and the shell provides only the grid cell around them.

Render boundaries are deliberate:

- `Routes`, `MainLayout`, Home, the authenticated summary, and Identity pages
  use static SSR. `Counter` opts into `InteractiveAuto` because its button needs
  .NET event handling. Counter actions are disabled during prerendering until
  their renderer becomes interactive. Static pages don't start a .NET interactive
  runtime just to display navigation.
- Shell links use Blazor's enhanced navigation: the server renders the destination
  and Blazor updates the existing document without restarting the page or
  reloading the runtime. The shell grows with its content and uses document
  scrolling, so Blazor resets and restores scroll position across navigation.
- Identity forms retain native inputs, submit buttons, form names, antiforgery,
  and passkey hooks so static POST mapping, autofill, and cookie redirects keep
  working. These POSTs keep normal navigation so cookie changes refresh
  authentication state. Shared notices retain their `notice`/`data-kind` contract.
  The reconnect/error UI retains native controls because it must function while the
  Blazor circuit is unavailable.

Do not reintroduce Bootstrap or utility classes, add `!important`, or add
ornamental animations. Keep form labels before their controls and preserve the
Blazor/Identity behavior hooks when editing markup. See the
[repository instructions](.github/copilot-instructions.md) and the
[UI instructions](.github/instructions/ui.instructions.md) for the authoring
conventions.

References: [CSS Grid layout](https://developer.mozilla.org/docs/Web/CSS/CSS_grid_layout)
and [enhanced navigation](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0#enhanced-navigation-and-form-handling).

## Resource graph and database

```text
postgres (PostgreSQL 18.3, managed data volume)
  ├─ ruventsdb (physical database: ruvents)
  │    └─ ruvents-migrations (EF database update)
  │         └─ ruvents-web (Blazor server + hosted WebAssembly)
  └─ pgadmin (explicit start)
```

After a normal startup, these dashboard states are expected:

| Resource | Expected state | Meaning |
| --- | --- | --- |
| `postgres`, `ruventsdb` | Running / Healthy | PostgreSQL and the application database are ready. |
| `ruvents-migrations` | Finished | The one-shot migration command completed successfully. It is not a long-running service. |
| `ruvents-web` | Running / Healthy | The web application is ready and its database readiness check passes. |
| `pgadmin` | Not started | Optional database UI; start it when needed. |

With hidden resources displayed, `ruvents-web-rebuilder` can also be **Not started**
until the web project's Rebuild command is used, and the EF tool resource can be
**Finished**. These helper states do not indicate an application startup failure.
An **Exited**, **Failed**, or **Unhealthy** application/database resource, or web
startup blocked by failed migrations, does require investigation through its logs.

The PostgreSQL image version is the built-in default of
`Aspire.Hosting.PostgreSQL` 13.6.4. Container lifetime remains Aspire's default
session lifetime. Data uses an Aspire-managed named volume rather than a repository
file. This is a fresh PostgreSQL schema; the original SQLite scaffold is removed.

Aspire supplies `ConnectionStrings:ruventsdb` to both the application and migration
tool. The application uses Npgsql EF Core 10.0.3, EF Core 10.0.14, and Aspire's
Npgsql EF integration 13.6.4. Its non-pooled `IDbContextFactory<ApplicationDbContext>`
supports a context per Blazor operation; Identity can still resolve the scoped
context. Dispose factory-created contexts with `await using`.

Identity schema version 3 is retained, including the `AspNetUserPasskeys` table.
Registration does not send email: accounts activate immediately, and every account is
required to upload a profile photo during registration before the rest of the
application becomes available. Password resets are an administrator action rather than a
self-service email flow. This setup does not configure a production email service.

A hosted deployment keeps the no-op sender. It defines `Platform:AdminEmail` and ships
the manual `ruvents-admin-bootstrap` job, which confirms and promotes that account
after the operator registers it (see [Deployment](#deployment)). The Development-only
confirmation link is never rendered in a hosted environment, so registration alone
cannot promote an administrator. Replacing `IdentityNoOpEmailSender` with a real email
service remains a separate decision for self-service confirmation and password reset;
keep the identity contract if that is introduced. External-login provider credentials,
a stable custom domain, and any further health-endpoint exposure remain
application-specific decisions.

## EF migrations

`Aspire.Hosting.EntityFrameworkCore` **13.6.4-preview.1.26523.11** manages
`ruvents-migrations` and its **dotnet-ef 10.0.14** tool. It does not alter the machine's
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
independently of PostgreSQL. Hosted environments opt in with
`HealthChecks__ExposeEndpoints=true`, which the deployment sets so the container app
probes reach those paths. Aspire monitors `/health`. The database readiness check
has a five-second timeout so EF's transient retries do not hold an unhealthy response
open for minutes. Cancellation is cooperative: an in-flight Npgsql connection attempt
can delay the response (about 15 seconds in local outage validation). Normal
application operations retain Aspire's retry behavior.

`src/Ruvents/Ruvents.http` lists the Development-only probes and the
starter's public pages for manual HTTP checks. Replace its port placeholder with
the endpoint `aspire describe` reports. This starter ships no JSON API; add
requests there as endpoints are added. OpenAPI and problem details are wired for
that work: in Development, `/openapi/v1.json` serves a document for whatever
endpoints exist, and `IProblemDetailsService` is registered for API error
responses. Exposing or authorizing the document in a deployed application is an
application-specific decision. Never commit cookies or credentials.

The dashboard exposes server logs, request traces, Npgsql database spans, and metrics.
Useful CLI commands include `aspire logs ruvents-web`, `aspire otel traces`, and
`aspire otel logs`. The Aspire MCP server provides the same resource and telemetry
visibility to agents. Database query telemetry can contain application information;
keep exports and runtime logs out of Git.

Aspire 13.6 retains dashboard run history automatically in **Run** persistence
mode. Use the header's run selector to compare the live run with completed runs;
historical runs are read-only. Up to ten unpinned runs are retained per application,
and pinning keeps a useful run. Console logs are only persisted after their stream
is viewed or exported, so capture needed console output before stopping.

Keep the default dashboard data directory, `<ASPIRE_HOME>/dashboard` (normally
`~/.aspire/dashboard`), outside the checkout. Persisted resource snapshots can
contain unredacted credentials even when the dashboard masks them. On Windows,
the directory inherits filesystem permissions; keep it private and do not share
its databases or backups. The existing Git exclusions cover `.aspire/`, `*.db`,
`*.db-wal`, and `*.db-shm`. No application telemetry changes are needed for run
history. See [dashboard persistence](https://aspire.dev/dashboard/data-persistence/).

For quick SQL inspection, select **REPL** on the running `postgres` resource. The
dashboard opens the container's bundled `psql` with its existing credentials, so
no local client or pgAdmin startup is required. Run `\connect ruvents` to switch
from the initial `postgres` database to the application database. Exit with `\q`
before closing the tab; closing the viewer alone can leave the client running.
This shell has normal write permissions, not read-only access. Database reset or
drop still requires explicit intent to delete local data. `WithRepl()` uses our
existing PostgreSQL package without an experimental-warning suppression, while
the dashboard terminal infrastructure remains preview. See
[PostgreSQL REPLs](https://aspire.dev/integrations/databases/postgres/postgres-host/#open-an-interactive-repl).

pgAdmin is available only in run mode. Start `pgadmin` from its dashboard action or:

```powershell
aspire resource pgadmin start --non-interactive
aspire wait pgadmin --timeout 120 --non-interactive
```

Open its generated HTTP endpoint, expand **Servers → postgres → Databases → ruvents**.
Aspire configures the connection and credentials. Stop it from the dashboard when done.

## Progressive web app

The server imports `build/PwaAssets.targets`, which generates a versioned
`service-worker.js` from hashed application inputs and serves it at a stable URL.
Installing from the browser adds the app to the home screen or desktop; the
install affordance is hidden when the app already runs standalone.

The worker caches only three public fallback assets (`offline.html`,
`pwa/offline.css`, `icons/icon-192.png`). Navigations are never cached: when a
page load or an enhanced-navigation GET fails, the app serves the offline screen
with a `503` so an interrupted load cannot be mistaken for a successful
operation. Signing in and saving data always need the server.

The update notice appears only when a newer worker is waiting while an older worker
still controls the page, so a first visit — installed or not — never reports a "new
version". Choosing **Later** keeps it hidden for the rest of the browser session, and
a subsequently published version offers it again. Nothing reloads until you choose
**Reload to update**, and only the tab that asked reloads; other tabs keep their
state and prompt separately. See
[build/README.md](build/README.md#pwa-build-assets) for generation rules and the
[UI instructions](.github/instructions/ui.instructions.md) for the constraints.

Verify a publish includes the worker, manifest, icons, and fallback assets:

```powershell
dotnet publish src/Ruvents/Ruvents.csproj --configuration Release --output artifacts/pwa-publish
```

## Agent instructions, tools, and skills

Agent instructions are GitHub Copilot only. Repository-wide rules live in
[`.github/copilot-instructions.md`](.github/copilot-instructions.md), and
path-scoped rules live in
[`.github/instructions/*.instructions.md`](.github/instructions) with `applyTo`
globs. Copilot loads a path-scoped file only when a file in scope matches, so the
repository-wide file names every rule-owning file. Where a rule belongs, and which
instruction sources Copilot discovers, is documented in
[agent hook maintenance](build/agent-hooks.md#instruction-sources-and-scope).
There is no `AGENTS.md`, `CLAUDE.md`, or `GEMINI.md`; do not add one. Instruction
changes are not picked up by an active session — restart it.

Project hooks are registered in `.github/hooks/*.json`:
`documentation-review.json` runs the documentation review reminder and finishing
pass, and `validation.json` provides the opt-in Fast validation described in
[agent hook maintenance](build/agent-hooks.md). Hooks are advisory, not commit gates.

Shared repository skills live only in `.agents/skills`, which Copilot discovers.
The Aspire skills use the first-party **aspire-skills v0.0.3** bundle refreshed
with CLI 13.6.0. Local corrections document 13.6's Project v2 diagnostic changes
and the distinction between persistent-resource cleanup and volume deletion.
Reconcile those corrections when refreshing the skills; use installed package/API
evidence and current documentation when upstream guidance differs. Project v2
migration remains deferred; the AppHost continues to use `AddProject`.

The repository registers the Aspire MCP server in [`.mcp.json`](.mcp.json) with
`aspire agent mcp`. Keep a single entry per agent; do not add a duplicate user-level
entry for a machine already using this repository's registration. To use a
user-level entry instead, add a local/stdio server named `aspire` with command
`aspire` and arguments `agent mcp` at user scope through Copilot's MCP settings or
`/mcp add` in the CLI. Restart/reload the agent after configuration. Start the
application from this repository and use the Aspire MCP resource-list tool to
verify the connection. If several AppHosts are running, select this repository's
AppHost explicitly.

The checked-in skills require no additional installation on clone. To refresh the
Aspire skills, use `aspire agent init` with the **standard** skill location
(`.agents/skills`), then review the diff. Avoid creating alternate copies under
`.github` or VS Code agent configuration. Keep user secrets, dashboard tokens,
runtime `.aspire` state, telemetry exports, Playwright CLI configuration and
output, and temporary browser output out of source control.

References: [Aspire MCP](https://aspire.dev/reference/cli/commands/aspire-agent-mcp/),
[Copilot CLI skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills),
[Copilot custom instructions](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions),
[Copilot desktop](https://docs.github.com/en/copilot/how-tos/github-copilot-app/customize-github-copilot-app).

## Deployment

Deployment targets **Azure Container Apps** in `Production`. `aspire publish` produces
the artifacts without touching Azure, and `ci.yml` validates them on pull requests that
change publish-relevant inputs. `.github/workflows/deploy.yml` is manual only
(`workflow_dispatch`), serializes by deployment target, and is gated on the repository
variable `DEPLOY_ENABLED`; until that variable is `true`, only the artifact-validation
job runs.

### One-time Azure and GitHub setup

Sign in with `az login` and `gh auth login`, then run from the repository root:

```powershell
pwsh ./scripts/Initialize-AzureDeployment.ps1 `
  -SubscriptionId '<azure-subscription-id>' -Location '<azure-region>' `
  -AdminEmail '<administrator@example.com>'
```

The script creates `rg-ruvents-production` and `id-ruvents-github`, registers the
required Azure providers (including `Microsoft.Storage`), and grants the deployment
identity **Contributor** and **Role Based Access Control Administrator** within that
resource group only. It reads GitHub's effective OIDC subject prefix and appends
`:environment:production` for Azure trust, including immutable owner/repository IDs
when enabled.

It creates the GitHub `production` environment with a `main`-only policy and writes
these environment variables: `AZURE_SUBSCRIPTION_ID`, `AZURE_TENANT_ID`,
`AZURE_CLIENT_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`, and
`PLATFORM_ADMIN_EMAIL`. It also sets the repository variable `DEPLOY_ENABLED=true`,
because the workflow reads that at job level before the environment is applied. No
client secret or publish profile is needed.

Protect `main` with the required checks, and consider required reviewers on the
`production` environment: the deployment identity can change every resource in the
resource group.

### What gets provisioned

- PostgreSQL Flexible Server 18 (Burstable B1ms, 32 GiB, seven-day backups,
  Entra-only authentication, no high availability). Its firewall permits Azure
  services; managed identity controls database access.
- A storage account for profile photos and event covers, with shared-key access
  disabled and Blob/Table/Queue Data Contributor granted to the application identity.
- Log Analytics and Application Insights. `ServiceDefaults` exports to Application
  Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present; local runs keep
  using OTLP.
- A Container Apps environment with ACR Basic and the managed Aspire dashboard.
- `ruvents-web` with external HTTPS ingress, one replica, and `/alive` and `/health`
  probes. Container Apps configures Data Protection storage automatically, so keep it
  stable across revisions.
- `ruvents-migrations`, a manual job that runs the EF bundle. The deployment pipeline
  waits for that execution before provisioning the new web revision.
- `ruvents-admin-bootstrap`, a manual job that confirms and promotes the configured
  platform administrator.

These are small initial production settings, not a high-availability configuration.

### First administrator

Production keeps the no-op email sender, so account confirmation is an operator
action. After the first deployment:

1. Register an account with the `PLATFORM_ADMIN_EMAIL` address.
2. Start the job: `az containerapp job start --name ruvents-admin-bootstrap --resource-group rg-ruvents-production`.
3. Sign in. The job sets `EmailConfirmed` and grants the platform `Admin` role. It is
   idempotent and exits without serving HTTP.

Anyone can register that address, so register it immediately after the first
deployment and run the job before inviting other users. Replacing the no-op sender
with a real email service remains a separate product decision.

### Deployment sequence and caveats

The deploy job signs in with OIDC and runs
`aspire deploy --environment Production --non-interactive`, then verifies `/alive`,
`/health`, and `/Account/Login` and writes the application URL to the run summary.
The migration job must succeed before the new web revision is provisioned, so schema
changes must remain compatible with the revision that is still serving.

Deployment failure does not roll back database changes; restore and destructive
migrations require a deliberate operator decision. A canceled run can leave a job
running in Azure. Attach a stable custom domain before registering production
passkeys, because passkeys are bound to the site origin.

For a local operator deployment after bootstrap, sign in to Azure and set:

```powershell
$env:Azure__SubscriptionId = '<azure-subscription-id>'
$env:Azure__ResourceGroup = 'rg-ruvents-production'
$env:Azure__Location = '<azure-region>'
$env:Azure__CredentialSource = 'AzureCli'
$env:Azure__AllowResourceGroupCreation = 'false'
$env:Platform__AdminEmail = '<administrator@example.com>'
aspire publish --environment Production --output-path "$env:TEMP/ruvents-preview" --non-interactive
aspire deploy --environment Production --non-interactive
```

Keep generated artifacts, deployment state, and telemetry out of Git and public
workflow artifacts. Local deploys must not overlap a GitHub deploy.

Bundle generation runs before Azure resolves the database connection string, so the
AppHost sets `Ruvents__DesignTime=true` on the migration tool only. With that flag the
application builds its model against a provider-only placeholder instead of failing;
a running application never sets it, so a missing connection string still fails fast.

References: [Aspire Container Apps](https://aspire.dev/deployment/azure/container-apps/),
[Aspire CI/CD](https://aspire.dev/deployment/ci-cd/),
[EF bundles](https://aspire.dev/integrations/databases/efcore/migrations/),
[GitHub OIDC with Azure](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-azure).

## Validation

The shared entry point selects the right checks for a scope and stops at the
first failure:

```powershell
pwsh ./scripts/Invoke-Validation.ps1 -Scope Quick          # staged whitespace checks only
pwsh ./scripts/Invoke-Validation.ps1 -Scope Fast           # format verify, build, unit + component tests
& ./scripts/Invoke-Validation.ps1 -Scope Infrastructure -Suite Integration,Aspire
pwsh ./scripts/Invoke-Validation.ps1 -Scope Full -Configuration Release
```

`Fast` is the default and needs no Docker, browser, or running Aspire instance.
Git hooks are opt-in and off by default; see
[build/README.md](build/README.md#local-validation-and-opt-in-git-hooks) to enable
them and for the full scope table.

For every change set, run formatting from the repository root, review the fixes,
and require a clean verification pass. Stop Aspire first on Windows. After code
changes, also build and run the unit and bUnit projects. All five test projects use
native Microsoft.Testing.Platform and Shouldly. The complete suite additionally
requires Docker, Aspire tooling, development HTTPS, and Playwright Chromium:

```powershell
dotnet format Ruvents.slnx --severity warn
dotnet format Ruvents.slnx --severity warn --verify-no-changes
dotnet build Ruvents.slnx
pwsh ./tests/Ruvents.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --solution Ruvents.slnx
```

The unit and component projects can still run individually without external
processes. Testcontainers covers focused database integration, Aspire integration
covers the real resource graph and HTTP behavior, and Playwright covers browser
interaction against its own isolated Aspire application. Test resources are
disposable and never use the development database volumes. See
[tests/README.md](tests/README.md) for commands, prerequisites, layer selection, and
[build/README.md](build/README.md) for formatting scope, analyzer rules, and Razor
code-behind validation. Formatting does not replace checks for other file types.
