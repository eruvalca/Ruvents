#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string] $Version)

. (Join-Path $PSScriptRoot 'template/Template.Common.ps1')
$repository = Split-Path $PSScriptRoot -Parent
Push-Location $repository
try {
    $status = & git status --porcelain --untracked-files=all
    Assert-Template ($LASTEXITCODE -eq 0) 'Cannot inspect Git status.'
    Assert-Template (-not $status) 'Commit all tracked/untracked changes before packing. Ignored build artifacts are allowed.'
    $commit = (& git rev-parse HEAD).Trim()
    Assert-Template ($LASTEXITCODE -eq 0 -and $commit -match '^[a-f0-9]{40}$') 'Cannot resolve source commit.'
    $artifactRoot = Join-Path $repository 'artifacts/templates'
    [IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
    $packagePath = Join-Path $artifactRoot "Ruvents.Templates.$Version.nupkg"
    Assert-Template (-not (Test-Path -LiteralPath $packagePath)) "Package already exists: $packagePath. Use a new version or explicitly remove the old candidate."
    $stage = Join-Path $artifactRoot ('stage-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    $export = Join-Path $stage 'committed-source.zip'
    Invoke-TemplateCommand git @('archive', '--format=zip', "--output=$export", $commit)
    $source = Join-Path $stage 'source'
    [IO.Compression.ZipFile]::ExtractToDirectory($export, $source)
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $source 'templates/content-manifest.json') | ConvertFrom-Json
    $config = Get-Content -Raw -LiteralPath (Join-Path $source '.template.config/template.json') | ConvertFrom-Json
    $excluded = Get-TemplateExcludes $config
    $content = Join-Path $stage 'content'
    [IO.Directory]::CreateDirectory($content) | Out-Null
    foreach ($required in $manifest.files) {
        Assert-Template (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf) "Required committed file is missing: $required"
    }
    foreach ($path in (Get-TemplateFiles $source)) {
        $selected = $path -cin $manifest.files
        foreach ($directory in $manifest.directories) { $selected = $selected -or $path.StartsWith($directory, [StringComparison]::Ordinal) }
        if (-not $selected -or (Test-TemplateGlob $path $excluded)) { continue }
        $origin = Join-Path $source $path
        Assert-Template (-not ((Get-Item -LiteralPath $origin).Attributes -band [IO.FileAttributes]::ReparsePoint)) "Links are unsupported: $path"
        $destination = Join-Path $content $path
        [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
        $extension = [IO.Path]::GetExtension($path)
        if ($extension -in $manifest.binaryExtensions) {
            Assert-Template (Test-TemplateGlob $path $config.sources[0].copyOnly) "Binary file needs copyOnly: $path"
            [IO.File]::Copy($origin, $destination)
        }
        else {
            Assert-Template ($extension -in $manifest.textExtensions -or $path -cin $manifest.files) "Classify this new file extension in templates/content-manifest.json: $path"
            $text = [IO.File]::ReadAllText($origin, [Text.UTF8Encoding]::new($false, $true))
            [IO.File]::WriteAllText($destination, (ConvertTo-TemplateSource $path $text))
        }
    }
    $provenance = [ordered]@{ templateIdentity = 'Ruvents.Solution'; packageVersion = $Version; sourceCommit = $commit }
    [IO.File]::WriteAllText((Join-Path $content '.template-provenance.json'), (($provenance | ConvertTo-Json) + "`n"))
    $inventory = [ordered]@{
        files = @(foreach ($path in (Get-TemplateFiles $content)) {
            @{ path = $path; sha256 = (Get-FileHash -LiteralPath (Join-Path $content $path) -Algorithm SHA256).Hash; binary = ([IO.Path]::GetExtension($path) -in $manifest.binaryExtensions) }
        })
    }
    [IO.File]::WriteAllText((Join-Path $content '.template.config/content-manifest.json'), (($inventory | ConvertTo-Json -Depth 5) + "`n"))
    # Pack the exported project as well: packaging inputs must come from the same commit.
    Invoke-TemplateCommand dotnet @('pack', (Join-Path $source 'templates/Ruvents.Templates.csproj'), "-p:PackageVersion=$Version", "-p:TemplateContentRoot=$content", '--output', $stage, '--nologo')
    $candidate = Join-Path $stage "Ruvents.Templates.$Version.nupkg"
    $package = Read-TemplatePackage $candidate
    [IO.File]::Move($candidate, $packagePath)
    Write-Host "Packed $($package.Inventory.files.Count) files from $commit."
    Write-Host "Package: $packagePath"
    Write-Host "Staging retained for inspection: $stage"
}
finally { Pop-Location }
