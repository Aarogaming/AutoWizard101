param(
    [Parameter(Mandatory = $true)][string]$Payload,
    [switch]$NoRestore
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payloadPath = (Resolve-Path -LiteralPath $Payload).Path
if ([IO.Path]::GetExtension($payloadPath) -ne '.zip') { throw 'Choose a ZIP from build_desktop.ps1 -Zip.' }
$output = Join-Path $repo ('.local/releases/Setup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $output | Out-Null
$restoreArgs = @(if ($NoRestore) { '--no-restore' })
& dotnet publish (Join-Path $repo 'Installer/Installer.csproj') -c Release -r win-x64 --self-contained true -m:1 -p:UseSharedCompilation=false -p:PublishSingleFile=true "-p:InstallerPayload=$payloadPath" -o $output @restoreArgs
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
$exe = Join-Path $output 'MaelstromSetup.exe'
Get-FileHash -LiteralPath $exe -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  MaelstromSetup.exe" } | Set-Content -LiteralPath (Join-Path $output 'SHA256.txt')
Write-Output "Installer: $exe"
