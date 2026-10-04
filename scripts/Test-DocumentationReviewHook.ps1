#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot
$hookScript = Join-Path $PSScriptRoot 'Invoke-DocumentationReviewHook.ps1'
$fixtureRoot = Join-Path $repoRoot ('artifacts/documentation-hook-tests/' + [guid]::NewGuid().ToString('N') + '/workspace with spaces')
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$passed = 0

function Invoke-FixtureGit([string[]] $Arguments) {
    $output = & git -C $fixtureRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture Git command failed: $($output -join "`n")"
    }
}

function Set-FixtureFile([string] $Path, [string] $Content) {
    $fullPath = Join-Path $fixtureRoot $Path
    New-Item -ItemType Directory -Path (Split-Path $fullPath) -Force | Out-Null
    [IO.File]::WriteAllText($fullPath, $Content)
}

function Invoke-Hook([string] $EventName, [string] $Turn, [hashtable] $Extra = @{}) {
    $payload = @{
        hook_event_name = $EventName
        session_id = 'fixture-session'
        turn_id = $Turn
        cwd = $fixtureRoot
        permission_mode = 'default'
    }
    foreach ($key in $Extra.Keys) {
        $payload[$key] = $Extra[$key]
    }
    $output = $payload | ConvertTo-Json -Compress | & pwsh -NoProfile -NonInteractive -File $hookScript
    if ($LASTEXITCODE -ne 0) {
        throw "Hook exited $LASTEXITCODE."
    }
    return ($output -join "`n") | ConvertFrom-Json -AsHashtable
}

function Assert-Result([string] $Name, [bool] $Condition) {
    if (-not $Condition) {
        throw "FAIL: $Name"
    }
    $script:passed++
    Write-Host "PASS: $Name"
}

function Invoke-WindowsManifestHook($Handler, [hashtable] $Payload, [string] $Shell) {
    $json = $Payload | ConvertTo-Json -Compress
    $output = if ($Shell -eq 'cmd') {
        $json | & $env:ComSpec /d /s /c $Handler.commandWindows
    }
    else {
        $json | & $Shell -NoProfile -NonInteractive -Command $Handler.commandWindows
    }
    return @{ ExitCode = $LASTEXITCODE; Output = $output -join "`n" }
}

# All mutations and commits below belong to this isolated, ignored test repo.
Invoke-FixtureGit @('init', '--quiet')
Invoke-FixtureGit @('config', 'user.name', 'Hook fixture')
Invoke-FixtureGit @('config', 'user.email', 'hook-fixture@example.invalid')
Invoke-FixtureGit @('config', 'commit.gpgsign', 'false')
Invoke-FixtureGit @('config', 'core.hooksPath', '.disabled-hooks')
Set-FixtureFile '.gitignore' "artifacts/`n"
Set-FixtureFile 'README.md' "# Fixture`n"

$result = Invoke-Hook 'UserPromptSubmit' 'unborn'
Assert-Result 'Initial repository receives pre-commit review instructions' (
    $result.hookSpecificOutput.additionalContext -match 'Before any authorized commit')
Assert-Result 'Early reminder explains the final-response completion contract' (
    $result.hookSpecificOutput.additionalContext.Contains('Documentation review: complete.'))
Assert-Result 'Unchanged untracked files do not trigger a finishing pass' (
    (Invoke-Hook 'Stop' 'unborn').Count -eq 0)
Set-FixtureFile 'new file.md' "# New document`n"
Assert-Result 'New untracked file triggers a review' (
    (Invoke-Hook 'Stop' 'unborn').decision -eq 'block')
Assert-Result 'Repeated Stop does not request another pass' (
    (Invoke-Hook 'Stop' 'unborn').Count -eq 0)

Invoke-FixtureGit @('add', '--all')
Invoke-FixtureGit @('commit', '--quiet', '-m', 'Fixture baseline')
Invoke-Hook 'UserPromptSubmit' 'clean' | Out-Null
Assert-Result 'Read-only turn in a clean repository stays quiet' (
    (Invoke-Hook 'Stop' 'clean').Count -eq 0)

Set-FixtureFile 'README.md' "# Existing dirty edit`n"
Invoke-Hook 'UserPromptSubmit' 'dirty' | Out-Null
Assert-Result 'Pre-existing dirty work alone stays quiet' (
    (Invoke-Hook 'Stop' 'dirty').Count -eq 0)
Set-FixtureFile 'README.md' "# Changed again in the turn`n"
$result = Invoke-Hook 'Stop' 'dirty'
Assert-Result 'Further edits to the same dirty file trigger review' (
    $result.decision -eq 'block' -and $result.reason -match 'README.md')

Invoke-Hook 'UserPromptSubmit' 'staging' | Out-Null
Invoke-FixtureGit @('add', 'README.md')
Assert-Result 'Index-only change is detected' (
    (Invoke-Hook 'Stop' 'staging').decision -eq 'block')
Invoke-Hook 'UserPromptSubmit' 'partial' | Out-Null
Set-FixtureFile 'README.md' "# Unstaged portion of an already-staged file`n"
Assert-Result 'Partial staging does not hide worktree edits' (
    (Invoke-Hook 'Stop' 'partial').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'commit' | Out-Null
Invoke-FixtureGit @('add', 'README.md')
Invoke-FixtureGit @('commit', '--quiet', '-m', 'Fixture change')
Assert-Result 'A commit during the turn still triggers review' (
    (Invoke-Hook 'Stop' 'commit').decision -eq 'block')

# A reported review can finish in the first pass, with or without doc edits.
$completedReports = @(
    'Documentation review: complete. Updated README.md for the new setup.'
    "Implemented the change.`r`n`r`nDocumentation review: complete. Existing guidance remains accurate; no updates needed."
    "``````text`nAn example without a report.`n```````nDocumentation review: complete. Reviewed AGENTS.md; no change needed."
)
for ($index = 0; $index -lt $completedReports.Count; $index++) {
    $turn = "completed-$index"
    Invoke-Hook 'UserPromptSubmit' $turn | Out-Null
    Set-FixtureFile 'source.cs' "class ReviewedFixture$index;"
    Assert-Result "Completed review $index avoids the first continuation" (
        (Invoke-Hook 'Stop' $turn @{ last_assistant_message = $completedReports[$index] }).Count -eq 0)
    Assert-Result "Completed review $index stays quiet when a repeated event omits the message" (
        (Invoke-Hook 'Stop' $turn).Count -eq 0)
}
Set-FixtureFile 'source.cs' 'class ChangedAfterReview;'
Assert-Result 'Later edits invalidate the remembered review fingerprint' (
    (Invoke-Hook 'Stop' $turn).decision -eq 'block')
Invoke-Hook 'UserPromptSubmit' 'after-completed-turn' | Out-Null
Set-FixtureFile 'source.cs' 'class ChangedInNextTurn;'
Assert-Result 'Review completion does not leak into a new turn' (
    (Invoke-Hook 'Stop' 'after-completed-turn').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'reviewed-commit' | Out-Null
Invoke-FixtureGit @('add', 'source.cs')
Invoke-FixtureGit @('commit', '--quiet', '-m', 'Reviewed fixture change')
Assert-Result 'A reported review also suppresses a redundant pass after a commit' (
    (Invoke-Hook 'Stop' 'reviewed-commit' @{ last_assistant_message = $completedReports[1] }).Count -eq 0)
Assert-Result 'Completion state stores no final-response text' (
    -not ((Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'artifacts/agent-hooks/documentation') -Filter '*.json' |
                Get-Content -Raw) -match 'Documentation review:|ReviewedFixture|ChangedInNextTurn'))

$incompleteReports = @(
    $null
    ''
    'I will review the documentation before finishing.'
    'Documentation review: pending. Still checking setup instructions.'
    'Documentation review: blocked. Could not read the relevant docs.'
    'Documentation review: incomplete. README.md still needs an update.'
    'Documentation review: complete.'
    'Use "Documentation review: complete. No changes needed." after reviewing.'
    '> Documentation review: complete. This is a quoted example.'
    '    Documentation review: complete. This is an indented code example.'
    "``````text`nDocumentation review: complete. This is a fenced example.`n``````"
    "~~~~text`n~~~`nDocumentation review: complete. A shorter fence did not close the example.`n~~~~"
    "``````text`n~~~`nDocumentation review: complete. A different fence did not close the example.`n``````"
)
for ($index = 0; $index -lt $incompleteReports.Count; $index++) {
    $turn = "incomplete-$index"
    Invoke-Hook 'UserPromptSubmit' $turn | Out-Null
    Set-FixtureFile 'source.cs' "class UnreviewedFixture$index;"
    Assert-Result "Missing, incomplete or example report $index still requests a review" (
        (Invoke-Hook 'Stop' $turn @{ last_assistant_message = $incompleteReports[$index] }).decision -eq 'block')
}

Invoke-Hook 'UserPromptSubmit' 'delete' | Out-Null
Remove-Item -LiteralPath (Join-Path $fixtureRoot 'new file.md')
Assert-Result 'Deleted files trigger review' (
    (Invoke-Hook 'Stop' 'delete').decision -eq 'block')

Invoke-Hook 'UserPromptSubmit' 'guard' | Out-Null
Set-FixtureFile 'source.cs' 'class Fixture;'
Assert-Result 'Codex stop_hook_active guard prevents continuation loops' (
    (Invoke-Hook 'Stop' 'guard' @{ stop_hook_active = $true }).Count -eq 0)
Assert-Result 'A different session cannot consume the baseline' (
    (Invoke-Hook 'Stop' 'guard' @{ session_id = 'other-session' }).Count -eq 0)
Assert-Result 'A missing baseline stays quiet' (
    (Invoke-Hook 'Stop' 'missing').Count -eq 0)
Assert-Result 'Plan-mode prompt does not set up a finishing pass' (
    (Invoke-Hook 'UserPromptSubmit' 'plan' @{ permission_mode = 'plan' }).Count -eq 0)
Assert-Result 'Plan-mode Stop never continues editing' (
    (Invoke-Hook 'Stop' 'guard' @{ permission_mode = 'plan' }).Count -eq 0)

# A duplicate prompt event must not overwrite the original comparison baseline.
Invoke-Hook 'UserPromptSubmit' 'duplicate' | Out-Null
Set-FixtureFile 'source.cs' 'class ChangedFixture;'
Invoke-Hook 'UserPromptSubmit' 'duplicate' | Out-Null
Assert-Result 'Duplicate start event preserves the original baseline' (
    (Invoke-Hook 'Stop' 'duplicate').decision -eq 'block')

$invalidJson = '{invalid' | & pwsh -NoProfile -NonInteractive -File $hookScript
Assert-Result 'Malformed input fails open with a JSON warning' (
    $LASTEXITCODE -eq 0 -and ($invalidJson | ConvertFrom-Json -AsHashtable).systemMessage)
$result = Invoke-Hook 'UserPromptSubmit' 'no-repo' @{ cwd = (Join-Path $fixtureRoot 'does-not-exist') }
Assert-Result 'Missing repository warns without dropping the early reminder' (
    $result.systemMessage -and $result.hookSpecificOutput.additionalContext)

# Exercise manifest commands through both Windows shell families. A cmd-only
# check misses PowerShell expansion of $variables inside nested quoted commands.
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot '.codex/hooks.json') -Raw | ConvertFrom-Json
$startHandler = $manifest.hooks.UserPromptSubmit[0].hooks[0]
$stopHandler = $manifest.hooks.Stop[0].hooks[0]
Assert-Result 'Manifest registers only the two approved documentation handlers' (
    @($manifest.hooks.PSObject.Properties).Count -eq 2 -and
    @($manifest.hooks.UserPromptSubmit).Count -eq 1 -and
    @($manifest.hooks.UserPromptSubmit[0].hooks).Count -eq 1 -and
    @($manifest.hooks.Stop).Count -eq 1 -and
    @($manifest.hooks.Stop[0].hooks).Count -eq 1 -and
    $startHandler.type -eq 'command' -and $stopHandler.type -eq 'command' -and
    $startHandler.command -match 'Invoke-DocumentationReviewHook.ps1' -and
    $startHandler.commandWindows -match 'Invoke-DocumentationReviewHook.ps1' -and
    $stopHandler.command -eq $startHandler.command -and
    $stopHandler.commandWindows -eq $startHandler.commandWindows)

if ($IsWindows) {
    $scriptDirectory = Join-Path $fixtureRoot 'scripts'
    New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
    Copy-Item -LiteralPath $hookScript -Destination $scriptDirectory
    Push-Location $scriptDirectory
    try {
        foreach ($shell in @('pwsh', 'powershell', 'cmd')) {
            $payload = @{
                hook_event_name = 'UserPromptSubmit'
                session_id = 'launcher'
                turn_id = $shell
                cwd = $scriptDirectory
            }
            $execution = Invoke-WindowsManifestHook $startHandler $payload $shell
            $result = $execution.Output | ConvertFrom-Json -AsHashtable
            Assert-Result "$shell prompt launcher resolves paths with spaces and preserves stdin" (
                $execution.ExitCode -eq 0 -and $result.hookSpecificOutput.additionalContext -and -not $result.systemMessage)

            $payload.hook_event_name = 'Stop'
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher stays quiet for an unchanged workspace" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
            Set-FixtureFile "launcher-$shell.md" 'A change after the manifest prompt hook.'
            $payload.last_assistant_message = $completedReports[0]
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher accepts a completed review after an edit" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
            $payload.Remove('last_assistant_message')
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher remembers a completed review" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
            Set-FixtureFile "launcher-$shell.md" 'Another change after the completed review.'
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher requests a review after an edit" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).decision -eq 'block')
            $execution = Invoke-WindowsManifestHook $stopHandler $payload $shell
            Assert-Result "$shell Stop launcher does not repeat the finishing pass" (
                $execution.ExitCode -eq 0 -and ($execution.Output | ConvertFrom-Json -AsHashtable).Count -eq 0)
        }
    }
    finally {
        Pop-Location
    }
}

Assert-Result 'Hook state is ignored by Git' (
    -not ((& git -C $fixtureRoot status --porcelain --untracked-files=all) -match 'artifacts/'))
Write-Host "Documentation hook checks: $passed passed, 0 failed, 0 skipped."
Write-Host "Fixture: $fixtureRoot"
