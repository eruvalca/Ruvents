# Maintaining the local solution template

Ruvents is both the runnable starter and the maintained source for the personal
`ruvents` template. There is no second copy of the application to keep in sync.
The package identity is `Ruvents.Templates`; the template identity is
`Ruvents.Solution`. Authoring tooling is outside `Ruvents.slnx` and is excluded
from generated applications.

## Create an application

After installation:

```powershell
dotnet new list ruvents
dotnet new ruvents --name MyNewApp --output D:\repos\MyNewApp
Set-Location D:\repos\MyNewApp
dotnet build MyNewApp.slnx
pwsh ./tests/MyNewApp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --solution MyNewApp.slnx
aspire run
```

Use a new or empty output directory. `dotnet new` keeps its normal overwrite
protection; do not routinely pass `--force`. Generation writes files and prints
instructions. It does not restore, initialize Git, start Aspire, configure an
agent, or trust certificates. IDEs may independently restore when opening a solution.

Supported names are PascalCase ASCII letters/digits, beginning with an uppercase
letter, **1–40 characters**. Examples: `Contoso`, `MyNewApp2`. Avoid Windows device
names (such as `Con`, `Nul`, `Com1`, `Lpt1`), dots, spaces, punctuation, and hyphens.
Output directories may contain spaces. This naming contract is documented, not
enforced by a custom template host. Unsupported names may generate broken project
references or invalid Aspire identifiers. The length limit leaves room for
database/resource suffixes and hostname labels; keep the overall path reasonably
short for Windows tooling.

For `MyNewApp`, project/namespace names use `MyNewApp`, the web resource and physical
database use `mynewapp`, the connection key uses `mynewappdb`, migrations use
`mynewapp-migrations`, and the hostname is `mynewapp.dev.localhost`. Ports stay
dynamic. Two independently generated applications receive different secrets IDs,
even with the same name. Use distinct names when running applications concurrently:
cookies are shared across ports on one hostname. Passkeys are bound to the site's
identity and are not portable application seed data.

## Pack, validate, install

Run PowerShell 7 commands from the Ruvents repository root. Git, the SDK selected
by `global.json`, and normal NuGet access are needed. Full validation also needs
Aspire CLI 13.6.0, Docker, trusted .NET development HTTPS and Playwright Chromium.
The validation script installs the matching Chromium binary after the first build
of each generated solution. Linux agents also need Playwright system dependencies.

1. Make the starter/authoring changes. Stop this checkout's Aspire instance before
   full builds. Run `dotnet build Ruvents.slnx` and
   `dotnet test --solution Ruvents.slnx`; run the Razor policy script when changing
   that policy or SDK. Check PowerShell syntax and review the content selection.
2. Commit the candidate. Packing refuses staged, unstaged, or untracked changes;
   ignored build output is allowed. Only the current commit is exported.
3. Choose a new three-part package version, then run:

   ```powershell
   pwsh ./scripts/Pack-Template.ps1 -Version 1.0.0
   pwsh ./scripts/Test-Template.ps1 -PackagePath ./artifacts/templates/Ruvents.Templates.1.0.0.nupkg
   ```

4. Complete the generated-application smoke check below, stop its Aspire instance,
   then install the tested package:

   ```powershell
   pwsh ./scripts/Install-Template.ps1 -PackagePath ./artifacts/templates/Ruvents.Templates.1.0.0.nupkg
   ```

The installer requires a successful full validation receipt with the exact package
SHA-256. Receipts prevent accidental use of an untested/replaced artifact; they
are local bookkeeping, not signed attestations. The runtime/browser smoke check
is recorded separately and is not implied by a successful script receipt.

If validation fails, fix the source, repeat affected checks, commit, and package
again. The packer refuses to overwrite an existing version. Use a new version or
explicitly remove a failed, never-installed candidate before retrying. Do not
reuse an installed version for different content. Installing a newer local
package with the same ID updates the registration. Retain old package files and
their receipts for rollback:

```powershell
dotnet new uninstall Ruvents.Templates
pwsh ./scripts/Install-Template.ps1 -PackagePath <retained-older-package.nupkg>
```

An installed `.nupkg` is cached by the template engine; it does not depend on the
staging directory or original checkout. No NuGet publication, remote repository,
push, or CI service is involved. Installing updates never changes applications
already generated. Do not use `dotnet new update` to refresh this private local
source snapshot; pack and install the next local version explicitly.

## Content and transformation rules

- `templates/content-manifest.json` is the source allowlist: whole application,
  test, build and checked-in skill directories, plus explicitly selected root,
  editor, browser and script files. New root configuration must be added deliberately.
- `.template.config/template.json` defines metadata, source naming, GUID replacement,
  copy-only assets, output exclusions, and one manual-instruction post-action.
  Source modifiers extend the template engine's exclusions. The packer also
  applies these exclusions before constructing the archive.
- The packaging project packs the exported commit, with no application compilation,
  assembly output or package dependencies. Required dotfiles are deliberately retained.
  Existing central analyzer versions/settings remain inherited; no new NuGet
  authoring dependencies or nested central package files are needed.
  The packaging project alone suppresses NU5110/NU5111: the included PowerShell
  policy script is template source, not a legacy NuGet installation hook.
- The initial Identity migration and snapshot are retained. Namespace and model
  type names change; migration identifiers and `RUV001`–`RUV004` stay stable.
- Binary assets are copied byte for byte. Unknown extensions fail packaging until
  classified in the manifest; add new binary formats to `copyOnly` too.
  PWA `.webmanifest` and `.html` files are classified as text so installed names
  and offline-page text receive application-name replacements. Worker cache
  prefixes also receive the lowercase name replacement. The SDK placeholder PNG
  icons remain byte-for-byte copies; generated applications supply their branding.
  The name-independent Razor policy script is also copy-only: its embedded C#
  `#if` fixture would otherwise be interpreted by the template engine. Packaging
  fails if an application name or secrets GUID is later added to copy-only text.
- For C# files containing compiler conditionals, staging adds template-engine
  `cnd:noEmit` control comments. They preserve all compiler branches while allowing
  name/GUID substitutions, and disappear during generation. No application source
  is edited by this step. A synthetic probe checks this behavior against the SDK.
  MSBuild files with `Condition` attributes receive analogous
  `msbuild-conditional:noEmit` guards: in particular, the engine would otherwise
  remove the Razor validator's item-list condition. Validation checks both forms.
- Only the marked authoring section is removed from the shared README. Historical
  acceptance results and all authoring docs/scripts are excluded entirely.
  `.template-provenance.json` records the template identity, version, and source
  commit and is copied without application-name replacement.
- An inventory inside `.template.config` records every packaged source file and its
  SHA-256. Validation checks the NuGet payload and compares every generated file
  against the expected substitutions. Configuration/inventory files do not appear
  in the generated solution.

Committed source selection prevents ignored files from leaking, but is not a
secret scanner. Keep secrets out of committed application settings. The denylist
also excludes local environment files, certificates, databases, runtime state,
binary build output, and authoring files even if accidentally committed in a
selected directory. `.env.example` is currently excluded too; add sanitized
examples deliberately if they become part of the starter.

## Automated validation

`Test-Template.ps1` uses a unique private template hive and generates applications
outside the source tree, under a unique temporary directory. It retains outputs
and command logs for inspection. It never changes the normal template registration.

It generates `TemplateSmoke`, `MyNewApp2` under a path containing spaces, `SameName`
twice in different directories, and `Ruvents` as a control. All files, naming,
solution references, fresh secrets, binary integrity and absence of post-generation
output are checked before any builds. The compiler-conditional probe is separate
and never enters the real package.

Both `TemplateSmoke` and `MyNewApp2` are built in Debug and Release, with all
five test projects run after each build, including isolated PostgreSQL, Aspire,
and browser tests. The Razor policy script is run in
both generated roots. Logs contain actual test totals and build errors. A generation
or build failure is reported separately from test execution.

`-StructureOnly` provides an authoring shortcut, but its receipt cannot authorize
installation. Full validation creates `<package>.validation.json` alongside the
package. Private hives, staging and generated directories are retained until you
remove their exact reported paths; stop generated Aspire instances first. Never
use a wildcard cleanup that could touch another application's checkout or database.

The private hive uses the template engine's debug switches, also used in upstream
template testing. Recheck this authoring interface when upgrading the SDK.

## Generated-application smoke check

Use `TemplateSmoke` from the full receipt's `runRoot`, never the original Ruvents
checkout. Generated secrets and the different AppHost path isolate application
state. Automated tests start and dispose isolated AppHosts; they do not populate
the generated application's development database. Start a separate development
instance for this manual workflow.

```powershell
aspire start --launch-profile https --non-interactive
aspire wait templatesmoke --timeout 120 --non-interactive
aspire describe --non-interactive
```

Verify PostgreSQL/database and the web resource are healthy, migrations are Finished,
and pgAdmin remains Not started. Discover the HTTPS endpoint from Aspire. In a
disposable browser session, register a disposable account, follow the scaffold
confirmation link, log in, update a profile field, log out, and log in again to
verify persistence. Check Home, Counter (including an increment), Auth Required,
navigation, CSS and JavaScript loading. Check `/health` and `/alive` return healthy
responses. Inspect `.vscode/settings.json`, `.vscode/launch.json` and
`aspire.config.json` for generated names/paths. Delete only the disposable account
through its account-management page, then stop this generated AppHost:

```powershell
aspire stop --non-interactive
```

PWA browser tests validate worker behavior and manifest names in generated apps.
Also inspect the generated `wwwroot/manifest.webmanifest`, offline title/text,
and worker cache prefixes for the chosen application name. Production installation
uses HTTPS and the production worker; see the generated README and tests/README.md
for the Development default and the `Pwa:EnableOfflineFallback` validation override.
An updated template package only changes future generated apps, including their
PWA files; it does not update an already installed PWA at an application's origin.

Do not attach to or reset the original Ruvents database. Persistent validation
volumes may be retained; removing a volume requires positively identifying that
validation volume. Do not infer browser passkey ceremonies, WebAssembly transitions,
full IDE debugging, or database outage behavior from headless tests. Report which
of these were actually exercised.

## Reproduced and external setup

The full Blazor architecture, sample pages, Identity/passkey implementation,
Fluent UI v5 components/icons and styles, migrations, five test suites and their
shared Aspire support library, build policies, `AGENTS.md`, checked-in skills,
VS Code settings and Playwright configuration are included. The baseline does
not set `AspireCliInvocationMode=DnxPinned`.

Machine prerequisites remain external: .NET SDK, Aspire CLI, Docker, PowerShell 7,
Edge/browser tooling, certificate trust, installed IDE extensions, agent plugins
and the existing user-level Aspire MCP entry. Most are reusable on this machine.
The first build/start still needs network access or populated package/tool/image
caches. The SDK has the existing `latestFeature` roll-forward policy, not an
embedded runtime; future upgrades require revalidation.

User secrets values, generated database credentials, accounts/passkeys/recovery
codes, database/pgAdmin data, editor session state, Git history/remotes, hosted
repositories, agent chats, and deployment infrastructure are not copied. Fresh
secret IDs do not populate credentials for external services. Configure production
email and remove or guard the no-op sender's confirmation shortcut before production;
its current behavior is deliberately preserved. Health endpoints remain Development-only.

## References

- [Custom .NET templates](https://learn.microsoft.com/en-us/dotnet/core/tools/templates)
- [Template packaging](https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-template-package)
- [Template configuration](https://github.com/dotnet/templating/wiki/Reference-for-template.json)
- [Conditional processing](https://github.com/dotnet/templating/wiki/Conditional-processing-and-comment-syntax)
- [Manual post-actions](https://github.com/dotnet/templating/wiki/Post-Action-Registry)
