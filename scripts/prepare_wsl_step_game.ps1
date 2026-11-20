param([string]$Workspace = (Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Workspace)
$source = Join-Path $root '.local/test-games/godot-dodge-the-creeps-4.7-6ad6167/source/dodge_the_creeps'
$destination = Join-Path $root '.local/wsl-dodge-step-game'
if (-not (Test-Path -LiteralPath (Join-Path $source 'project.godot') -PathType Leaf)) { throw 'The local Dodge source is missing.' }
if (Test-Path -LiteralPath $destination) { throw 'The stepped test copy already exists; it was not overwritten.' }
$main = Get-Content -LiteralPath (Join-Path $source 'main.gd') -Raw
if ($main -match '(?m)^func _ready\(') { throw 'The source already has a ready handler; refusing to modify it.' }
Copy-Item -LiteralPath $source -Destination $destination -Recurse
Add-Content -LiteralPath (Join-Path $destination 'main.gd') -Value (Get-Content -LiteralPath (Join-Path $root 'tools/fixtures/godot_dodge_step_bootstrap.gd') -Raw) -Encoding utf8
Copy-Item -LiteralPath (Join-Path $root 'tools/fixtures/godot_dodge_step_controller.gd') -Destination (Join-Path $destination 'maelstrom_step_controller.gd')
Write-Output "Prepared isolated stepped Dodge copy: $destination"
