---
applyTo:
  - "src/Ruvents.AppHost/**"
  - ".github/workflows/**"
---

# Aspire and infrastructure

Read this guide before application lifecycle, migration, publish, or Aspire
tooling operations, even when no AppHost files change. It also governs resource
wiring and related root configuration, publish workflows, and infrastructure test
orchestration. Database/migration dependencies and health contracts are in the
[server guide](server.instructions.md); read it before changing that wiring.
Consult the [test guide](tests.instructions.md) before running tests. Detailed
setup and runtime procedures remain in the [root README](../../README.md).

## Orchestration and developer resources

- Aspire is the default application run/debug entry point. Use the CLI for agent
  runs and the checked-in **Aspire: Ruvents** VS Code configuration for F5
  debugging; in Visual Studio, use `Ruvents.AppHost` as the startup project.
  Do not bypass orchestration by launching the web or WebAssembly project alone.
  Unit/component tests remain headless and do not require Aspire.
- Run Aspire CLI commands from the repository root; `aspire.config.json` selects
  `src/Ruvents.AppHost/Ruvents.AppHost.csproj`. Use
  `aspire start --launch-profile https --non-interactive`,
  `aspire wait ruvents-web --timeout 120 --non-interactive`, and `aspire describe`.
  Stop Aspire before formatting/full builds on Windows and when agent validation
  is finished. Ordinary shutdown uses `aspire stop`; `--force` removes persistent
  resource instances and requires explicit destructive intent for one AppHost.
  In 13.6, named volumes are preserved unless `--volumes` is also requested;
  never add either cleanup flag to routine validation.
- Keep the Aspire CLI workflow pin, the compatible Aspire package set including
  the explicitly selected `Aspire.Hosting.EntityFrameworkCore` preview, and the
  managed EF tool version in `AppHost.cs` aligned with each other and with the
  documented setup in `README.md`. The manifests own those versions; do not
  restate them here.
  Use `.agents/skills` as the shared skill location. The repository registers the
  Aspire MCP server in `.mcp.json`; keep one effective entry per agent and do not
  add a duplicate user-level server. Follow the registration procedure documented
  in `README.md`.
- pgAdmin is an explicit-start, run-mode developer tool. See `README.md` for
  endpoint discovery, data persistence, and telemetry. PostgreSQL's **REPL**
  command opens `psql`; use `\connect ruvents` to select the application
  database and `\q` to exit. It has normal write permissions. Database reset/drop
  requires an explicit intent to delete the local data. Do not add preview browser
  logging unless explicitly requested.
- The dashboard retains run history automatically. Keep persisted dashboard data
  in its user-local default directory and out of shared artifacts: it can contain
  unredacted credentials. View or export console logs before stopping if they are
  needed in historical runs; not every console stream is captured automatically.
- A normal run leaves pgAdmin **Not started**, migrations
  **Finished**, and the web/database resources **Running / Healthy**. Inspect
  resource commands and logs before treating optional or one-shot resource states
  as failures. Use existing Identity registration/login/account management to
  exercise PostgreSQL; remove only disposable accounts created for validation.

## Publish and workflows

- `aspire publish` runs the publish pipeline; the CI `publish` job validates the
  Container Apps artifacts and the migration bundle on pull requests that change
  publish-relevant inputs. The AppHost targets Azure Container Apps in `Production`.
  `scripts/Initialize-AzureDeployment.ps1` performs the one-time Azure and GitHub
  bootstrap, and the manual `deploy.yml` workflow runs `aspire deploy` behind the
  repository variable `DEPLOY_ENABLED`. Do not change the deployment identity's
  scope, add cloud-specific wiring, or weaken the migration gate without an explicit
  decision and the matching bootstrap and README update.
- The manual `ruvents-admin-bootstrap` job is the only supported out-of-band path for
  confirming the platform administrator. It runs the web project with
  `Ruvents__ConfirmAdmin=true`; never set that flag on `ruvents-web`. Keep
  `Platform__AdminEmail`, `PLATFORM_ADMIN_EMAIL`, and the documented first-admin
  procedure aligned.
- Workflows select work through `scripts/Get-ValidationScope.ps1`. Browser
  validation is opt-in per pull request through the `run-full-browser` label and
  is never merge-blocking; the nightly workflow owns the full browser and
  performance suites and reports failures through rolling issues.
  `scripts/Test-BrowserWorkflowPolicy.ps1` asserts these properties, so change the
  workflow and its policy test together.
