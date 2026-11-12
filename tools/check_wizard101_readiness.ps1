<#
.SYNOPSIS
Reports local Wizard101 integration files without launching or installing tools.
.EXAMPLE
pwsh -File tools/check_wizard101_readiness.ps1 -AsJson
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$library = 'ProjectMaelstrom/ProjectMaelstrom/Scripts/Library'
$definitions = @(
    @{ Name = 'Deimos-Wizard101'; Source = "$library/Deimos-Wizard101/Deimos.py"; Manifest = "$library/Deimos-Wizard101/manifest.json"; Role = 'External automation application' }
    @{ Name = 'wizwalker'; Source = "$library/wizwalker/wizwalker-master/pyproject.toml"; Manifest = "$library/wizwalker/manifest.json"; Role = 'Python backend library' }
    @{ Name = 'wizSDK'; Source = "$library/wizSDK/wizSDK-master/pyproject.toml"; Manifest = "$library/wizSDK/manifest.json"; Role = 'Legacy workflow library' }
    @{ Name = 'wizard101-companion'; Source = "$library/wizard101-companion/start.bat"; Role = 'Companion application candidate' }
    @{ Name = 'Wizard101_DanceBot'; Source = 'ProjectMaelstrom/ProjectMaelstrom/Scripts/Wizard101_DanceBot/dist/petdance/petdance.exe'; Role = 'Visual automation application' }
    @{ Name = 'moonlight'; Source = "$library/moonlight/pyproject.toml"; Role = 'Offline packet diagnostics' }
    @{ Name = 'wiztype'; Source = "$library/wiztype/pyproject.toml"; Role = 'Client type diagnostics' }
    @{ Name = 'wizdiff'; Source = "$library/wizdiff/pyproject.toml"; Role = 'Patch diagnostics' }
)

$results = foreach ($definition in $definitions) {
    $manifestState = 'Not cataloged'
    $entryPresent = $false
    $entryPoint = $null
    if ($definition.Manifest) {
        $manifestPath = Join-Path $root $definition.Manifest
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            try {
                $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                $manifestState = [string]$manifest.status
                $entryPoint = [string]$manifest.entryPoint
                if ($entryPoint) {
                    $entryPresent = Test-Path -LiteralPath (Join-Path (Split-Path -Parent $manifestPath) $entryPoint) -PathType Leaf
                }
            }
            catch {
                $manifestState = 'Invalid manifest'
            }
        }
        else {
            $manifestState = 'Manifest missing'
        }
    }
    [pscustomobject]@{
        Name = $definition.Name
        Role = $definition.Role
        SourceMarkerPresent = (Test-Path -LiteralPath (Join-Path $root $definition.Source) -PathType Leaf)
        ExpectedSourceMarker = $definition.Source
        ManifestStatus = $manifestState
        EntryPoint = $entryPoint
        EntryPointPresent = $entryPresent
        RuntimeCompatibility = 'Not tested'
    }
}

if ($AsJson) {
    ConvertTo-Json -InputObject @($results) -Depth 4
}
else {
    $results | Format-Table Name, SourceMarkerPresent, ManifestStatus, EntryPointPresent, RuntimeCompatibility -AutoSize
    Write-Output 'File presence does not establish installed dependencies, a working launcher, or game compatibility.'
    Write-Output 'Checks use the expected repository paths only; external installations are not detected.'
}
