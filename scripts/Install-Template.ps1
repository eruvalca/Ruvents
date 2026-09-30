#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $PackagePath)

. (Join-Path $PSScriptRoot 'template/Template.Common.ps1')
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$package = Read-TemplatePackage $PackagePath
$receiptPath = "$PackagePath.validation.json"
Assert-Template (Test-Path -LiteralPath $receiptPath) 'Run Test-Template.ps1 on this package before installing it.'
$receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
Assert-Template ($receipt.status -eq 'Passed' -and $receipt.level -eq 'Full') 'A successful full template validation is required.'
Assert-Template ($receipt.packageSha256 -ceq (Get-FileHash -LiteralPath $PackagePath).Hash) 'Package changed after validation. Validate it again.'
# A local package update replaces the earlier version with the same package ID.
# Do not force a collision with unrelated packages or silently uninstall anything.
Invoke-TemplateCommand dotnet @('new', 'install', $PackagePath)
Invoke-TemplateCommand dotnet @('new', 'list', 'ruvents')
Write-Host "Installed Ruvents.Templates $($package.Provenance.packageVersion)."
Write-Host 'Create: dotnet new ruvents --name MyNewApp --output D:\repos\MyNewApp'
