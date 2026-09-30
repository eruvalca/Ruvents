# Shared authoring helpers; never included in generated applications.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-TemplateCommand {
    param([string] $Command, [string[]] $Arguments, [string] $LogPath, [switch] $Capture)
    Write-Host "> $Command $($Arguments -join ' ')"
    $output = & $Command @Arguments 2>&1
    $code = $LASTEXITCODE
    $text = $output -join "`n"
    if ($LogPath) { [IO.File]::WriteAllText($LogPath, $text) }
    Write-Host $text
    if ($code -ne 0) { throw "$Command failed with exit code $code. See output above." }
    if ($Capture) { return $text }
}

function Assert-Template {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Get-TemplateFiles {
    param([string] $Root)
    return @(Get-ChildItem -LiteralPath $Root -File -Recurse -Force | ForEach-Object {
        [IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
    } | Sort-Object)
}

function Test-TemplateGlob {
    param([string] $Path, [string[]] $Patterns)
    foreach ($pattern in $Patterns) {
        $expression = [regex]::Escape($pattern).Replace('\*\*/', '(?:.*/)?').Replace('\*\*', '.*').Replace('\*', '[^/]*').Replace('\?', '[^/]')
        if ($Path -imatch "^$expression$") { return $true }
    }
    return $false
}

function Get-TemplateExcludes {
    param($Config)
    # The SDK defaults are retained by template.json; apply their file equivalents before packing too.
    return @('**/bin/**', '**/obj/**', '**/*.filelist', '**/*.user', '**/*.lock.json') + @($Config.sources[0].modifiers[0].exclude)
}

function ConvertTo-TemplateSource {
    param([string] $Path, [string] $Text)
    if ($Path -eq 'README.md') {
        $Text = [regex]::Replace($Text, '(?s)<!-- template-authoring:start -->.*?<!-- template-authoring:end -->\r?\n*', '')
    }
    if ($Path.EndsWith('.cs') -and $Text -match '(?m)^\s*#(?:if|elif|else|endif)\b') {
        # Disable only template conditional evaluation, retaining name/GUID replacement.
        # These control comments disappear from the generated file.
        Assert-Template (-not $Text.Contains(':cnd:')) "Review existing template conditional controls in $Path."
        $Text = "//-:cnd:noEmit`n" + $Text + "//+:cnd:noEmit`n"
    }
    if ($Path -match '\.(?:\w*proj|props|targets|msbuild)$' -and $Text -match '\bCondition\s*=') {
        Assert-Template (-not $Text.Contains(':msbuild-conditional:')) "Review existing MSBuild template controls in $Path."
        $Text = "<!--/-:msbuild-conditional:noEmit -->`n" + $Text + "<!--/+:msbuild-conditional:noEmit -->`n"
    }
    return $Text
}

function Read-TemplatePackage {
    param([string] $PackagePath)
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $files = [Collections.Generic.Dictionary[string, byte[]]]::new([StringComparer]::Ordinal)
        foreach ($entry in $archive.Entries) {
            $path = [Uri]::UnescapeDataString($entry.FullName)
            Assert-Template ($path -notmatch '(^/|\\|(^|/)\.\.(/|$))') "Unsafe package path: $path"
            if ($path.EndsWith('/')) { continue }
            $stream = $entry.Open()
            $buffer = [IO.MemoryStream]::new()
            try { $stream.CopyTo($buffer); $files.Add($path, $buffer.ToArray()) }
            finally { $buffer.Dispose(); $stream.Dispose() }
        }
        $prefix = 'content/Ruvents/'
        $config = [Text.Encoding]::UTF8.GetString($files[$prefix + '.template.config/template.json']) | ConvertFrom-Json
        $provenance = [Text.Encoding]::UTF8.GetString($files[$prefix + '.template-provenance.json']) | ConvertFrom-Json
        $inventory = [Text.Encoding]::UTF8.GetString($files[$prefix + '.template.config/content-manifest.json']) | ConvertFrom-Json
        [xml] $nuspec = [Text.Encoding]::UTF8.GetString($files['Ruvents.Templates.nuspec']).TrimStart([char]0xFEFF)
        Assert-Template ($nuspec.package.metadata.id -ceq 'Ruvents.Templates') 'Wrong package ID.'
        Assert-Template ($nuspec.package.metadata.version -ceq $provenance.packageVersion) 'Package/provenance versions disagree.'
        Assert-Template ($config.identity -ceq 'Ruvents.Solution' -and $provenance.templateIdentity -ceq 'Ruvents.Solution') 'Wrong template identity.'
        Assert-Template ($provenance.sourceCommit -match '^[a-f0-9]{40}$') 'Missing committed-source provenance.'
        Assert-Template ($config.postActions.Count -eq 1 -and $config.postActions[0].actionId -ieq 'AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C') 'Only manual post-actions are allowed.'
        $actual = @($files.Keys | Where-Object { $_.StartsWith($prefix) } | ForEach-Object { $_.Substring($prefix.Length) } | Sort-Object)
        $expected = @(@($inventory.files.path) + '.template.config/content-manifest.json' | Sort-Object)
        Assert-Template (-not (Compare-Object $expected $actual -CaseSensitive)) 'Package file list differs from the staged inventory.'
        $excluded = Get-TemplateExcludes $config
        foreach ($item in $inventory.files) {
            Assert-Template (-not (Test-TemplateGlob $item.path $excluded)) "Excluded file in package: $($item.path)"
            $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($files[$prefix + $item.path]))
            Assert-Template ($hash -ceq $item.sha256) "Package content hash mismatch: $($item.path)"
        }
        foreach ($path in $files.Keys) {
            if ($path.StartsWith($prefix)) { continue }
            Assert-Template ($path -eq 'Ruvents.Templates.nuspec' -or $path -eq 'PACKAGE-README.md' -or $path -eq '[Content_Types].xml' -or $path -eq '_rels/.rels' -or $path -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$') "Unexpected package payload: $path"
        }
        return [pscustomobject]@{ Files = $files; Config = $config; Provenance = $provenance; Inventory = $inventory; Prefix = $prefix }
    }
    finally { $archive.Dispose() }
}
