#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $PackagePath,
    # Useful during authoring; never produces an installable validation receipt.
    [switch] $StructureOnly
)

. (Join-Path $PSScriptRoot 'template/Template.Common.ps1')
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$package = Read-TemplatePackage $PackagePath
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ('ruvents-template-validation-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$repository = Split-Path $PSScriptRoot -Parent
Assert-Template (-not $runRoot.StartsWith($repository.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) 'Validation must run outside the source tree.'
$hive = Join-Path $runRoot 'hive'
$hiveArgs = @('--debug:custom-hive', $hive, '--debug:disable-sdk-templates')
$receiptPath = "$PackagePath.validation.json"
$receipt = [ordered]@{
    status = 'Running'
    level = $(if ($StructureOnly) { 'StructureOnly' } else { 'Full' })
    packageSha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
    packageVersion = $package.Provenance.packageVersion
    sourceCommit = $package.Provenance.sourceCommit
    sdkVersion = (& dotnet --version).Trim()
    runRoot = $runRoot
    generatedSolutions = @()
    commands = @()
    runtimeSmoke = 'Separate Aspire/browser validation required; not performed by this script.'
}

function Invoke-ValidationCommand {
    param([string] $Command, [string[]] $Arguments, [string] $LogName)
    $receipt.commands += @{ command = $Command; arguments = $Arguments; directory = (Get-Location).Path; log = $LogName }
    Invoke-TemplateCommand $Command $Arguments -LogPath (Join-Path $runRoot $LogName)
}

try {
    Push-Location $runRoot
    try {
        Invoke-ValidationCommand dotnet (@('new', 'install', $PackagePath) + $hiveArgs) 'install.log'
        $cases = @(
            @{ name = 'TemplateSmoke'; directory = 'TemplateSmoke' },
            @{ name = 'MyNewApp2'; directory = 'Parent With Spaces/MyNewApp2' },
            @{ name = 'SameName'; directory = 'first/SameName' },
            @{ name = 'SameName'; directory = 'second/SameName' },
            @{ name = 'Ruvents'; directory = 'Control' }
        )
        $seenGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $caseNumber = 0
        foreach ($case in $cases) {
            $caseNumber++
            $name = $case.name
            $directory = Join-Path $runRoot $case.directory
            Invoke-ValidationCommand dotnet (@('new', 'ruvents', '--name', $name, '--output', $directory) + $hiveArgs) "generate-$caseNumber.log"
            $receipt.generatedSolutions += @{ name = $name; directory = $directory }
            [xml] $appHost = Get-Content -Raw -LiteralPath (Join-Path $directory "src/$name.AppHost/$name.AppHost.csproj")
            [xml] $web = Get-Content -Raw -LiteralPath (Join-Path $directory "src/$name/$name.csproj")
            $hostGuid = [string] $appHost.SelectSingleNode('//UserSecretsId').InnerText
            $webId = [string] $web.SelectSingleNode('//UserSecretsId').InnerText
            Assert-Template ($webId.StartsWith("aspnet-$name-")) 'Web secrets ID was not renamed.'
            $webGuid = $webId.Substring("aspnet-$name-".Length)
            $guids = @($hostGuid, $webGuid)
            foreach ($guid in $guids) {
                Assert-Template ($guid -match '^[a-fA-F0-9]{8}(-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}$') 'Invalid generated secrets GUID.'
                Assert-Template ($guid -notin $package.Config.guids -and $seenGuids.Add($guid)) 'Secrets GUID reused across generated applications.'
            }
            $expectedPaths = [Collections.Generic.List[string]]::new()
            foreach ($entry in $package.Inventory.files) {
                if ($entry.path.StartsWith('.template.config/')) { continue }
                $renamed = $entry.path.Replace('Ruvents', $name).Replace('ruvents', $name.ToLowerInvariant())
                $expectedPaths.Add($renamed)
                $generatedPath = Join-Path $directory $renamed
                Assert-Template (Test-Path -LiteralPath $generatedPath -PathType Leaf) "Missing generated file: $renamed"
                $bytes = $package.Files[$package.Prefix + $entry.path]
                if ($entry.binary -or $entry.path -eq '.template-provenance.json') {
                    $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
                    Assert-Template ((Get-FileHash -LiteralPath $generatedPath).Hash -ceq $expectedHash) "Copy-only content changed: $renamed"
                    continue
                }
                $expectedText = [Text.Encoding]::UTF8.GetString($bytes).Replace('Ruvents', $name).Replace('ruvents', $name.ToLowerInvariant())
                $expectedText = $expectedText.Replace("//-:cnd:noEmit`n", '').Replace("//+:cnd:noEmit`n", '')
                for ($index = 0; $index -lt $guids.Count; $index++) {
                    $expectedText = $expectedText.Replace($package.Config.guids[$index], $guids[$index])
                }
                $actualText = [IO.File]::ReadAllText($generatedPath)
                Assert-Template ($actualText -ceq $expectedText) "Unexpected content transformation: $renamed"
                if ($name -ne 'Ruvents') {
                    Assert-Template ($actualText -notmatch '(?i)ruvents') "Unrenamed application reference: $renamed"
                }
            }
            $actualPaths = Get-TemplateFiles $directory
            Assert-Template (-not (Compare-Object @($expectedPaths | Sort-Object) $actualPaths -CaseSensitive)) 'Unexpected output files: possible restore, authoring content, or missing source.'
            [xml] $solution = Get-Content -Raw -LiteralPath (Join-Path $directory "$name.slnx")
            foreach ($project in $solution.SelectNodes('//Project')) {
                Assert-Template (Test-Path -LiteralPath (Join-Path $directory $project.Path)) "Broken solution reference: $($project.Path)"
            }
            Write-Host "PASS: $name in $($case.directory): $($actualPaths.Count) files; fresh secrets; exact content and naming."
        }

        # Exercise the same conditional-preservation transform used by the packer.
        $fixture = Join-Path $runRoot 'conditional-source'
        [IO.Directory]::CreateDirectory((Join-Path $fixture '.template.config')) | Out-Null
        $fixtureConfig = @{ identity = 'Ruvents.ConditionalProbe'; name = 'Conditional probe'; shortName = 'ruvents-conditional-probe'; sourceName = 'Ruvents' }
        [IO.File]::WriteAllText((Join-Path $fixture '.template.config/template.json'), ($fixtureConfig | ConvertTo-Json))
        $conditional = "namespace Ruvents;`n#if DEBUG`ninternal sealed class DebugType { }`n#else`ninternal sealed class ReleaseType { }`n#endif`n"
        [IO.File]::WriteAllText((Join-Path $fixture 'Probe.cs'), (ConvertTo-TemplateSource 'Probe.cs' $conditional))
        Invoke-ValidationCommand dotnet (@('new', 'install', $fixture) + $hiveArgs) 'conditional-install.log'
        $fixtureOutput = Join-Path $runRoot 'conditional-output'
        Invoke-ValidationCommand dotnet (@('new', 'ruvents-conditional-probe', '--name', 'ConditionalApp', '--output', $fixtureOutput) + $hiveArgs) 'conditional-generate.log'
        Assert-Template ([IO.File]::ReadAllText((Join-Path $fixtureOutput 'Probe.cs')) -ceq $conditional.Replace('Ruvents', 'ConditionalApp')) 'Compiler conditionals were altered or renaming was suppressed.'
        Write-Host 'PASS: compiler conditionals and name replacement coexist.'
    }
    finally { Pop-Location }

    if (-not $StructureOnly) {
        foreach ($case in $cases[0..1]) {
            $name = $case.name
            Push-Location (Join-Path $runRoot $case.directory)
            try {
                # These are fresh directories; no Aspire instance has been started here.
                foreach ($configuration in @('Debug', 'Release')) {
                    Invoke-ValidationCommand dotnet @('build', "$name.slnx", '--configuration', $configuration, '--nologo') "$name-$configuration-build.log"
                    Invoke-ValidationCommand dotnet @('test', '--solution', "$name.slnx", '--configuration', $configuration, '--no-build') "$name-$configuration-test.log"
                }
                Invoke-ValidationCommand pwsh @('-NoProfile', '-File', 'scripts/Test-RazorCodeBehind.ps1') "$name-razor-policy.log"
            }
            finally { Pop-Location }
        }
    }
    $receipt.status = 'Passed'
    Write-Host "PASS: $($receipt.level) template validation. Logs and generated solutions: $runRoot"
}
catch {
    $receipt.status = 'Failed'
    $receipt.error = $_.Exception.Message
    throw
}
finally {
    $receipt.completedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json -Depth 8) + "`n"))
    Write-Host "Validation receipt: $receiptPath"
}
