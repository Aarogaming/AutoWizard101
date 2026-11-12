param([string]$Workspace = (Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Workspace)
$source = Join-Path $root '.local/test-games/reflex-679450da/reaction-game-679450da34d8e4840115a8594a9d53f238724d5c'
$destination = Join-Path $root '.local/test-games/reflex-offline-679450da'
if (Test-Path -LiteralPath $destination) { throw "Choose a fresh destination or use the existing offline copy: $destination" }
$html = Get-Content -LiteralPath (Join-Path $source 'index.html') -Raw
$externalPattern = '(?m)^\s*<link[^\r\n]*https://fonts\.(?:googleapis|gstatic)\.com[^\r\n]*\r?\n|(?m)^\s*<script src="https://cdnjs\.cloudflare\.com/ajax/libs/three\.js/r128/three\.min\.js"></script>\s*'
$modified = [regex]::Replace($html, $externalPattern, '')
if ($modified -eq $html -or $modified -match '(?i)https?://') { throw 'Unexpected external resources; inspect the source before preparing it.' }
New-Item -ItemType Directory -Path $destination | Out-Null
Set-Content -LiteralPath (Join-Path $destination 'index.html') -Value $modified -Encoding utf8NoBOM
Copy-Item -LiteralPath (Join-Path $source 'favicon.png') -Destination $destination
Copy-Item -LiteralPath (Join-Path $source 'README.md') -Destination (Join-Path $destination 'UPSTREAM-README.md')
@'
Offline copy of Reflex — Reaction Time Lab, upstream commit 679450da34d8e4840115a8594a9d53f238724d5c.
The external font and Three.js links were removed. The unmodified game's built-in non-WebGL fallback is used.
Upstream source: https://github.com/ayannaautomates/reaction-game
See UPSTREAM-README.md for attribution and the author's MIT license statement.
'@ | Set-Content -LiteralPath (Join-Path $destination 'OFFLINE-NOTES.txt') -Encoding utf8NoBOM
Write-Output "Offline Reflex: $destination"
