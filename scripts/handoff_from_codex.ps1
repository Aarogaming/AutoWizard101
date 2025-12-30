$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { $root = Split-Path -Parent $PSScriptRoot }
Set-Location $root
dotnet run --project DevTools/HandoffBridge/HandoffBridge.csproj -- import --root "$root" @args
