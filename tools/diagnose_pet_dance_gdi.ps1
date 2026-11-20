param(
    [Parameter(Mandatory = $true)][string]$ProfilePath,
    [Parameter(Mandatory = $true)][string]$StudyRoot,
    [Parameter(Mandatory = $true)][string]$Output
)
# Offline only. Mirrors PetDancePanelVision.Extract with System.Drawing/GDI+;
# no application compilation, game/process attachment, capture or input.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$profileFile = (Resolve-Path -LiteralPath $ProfilePath).Path
$studyDirectory = (Resolve-Path -LiteralPath $StudyRoot).Path
$outputDirectory = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputDirectory) { throw 'Choose a new output directory.' }
$profile = Get-Content -LiteralPath $profileFile -Raw | ConvertFrom-Json
$pixelFormat = [Drawing.Imaging.PixelFormat]::Format24bppRgb
$canonicalWidth = [int]$profile.Width
$canonicalHeight = [int]$profile.Height
if ($canonicalWidth -ne 1282 -or $canonicalHeight -ne 760) { throw 'This diagnostic is scoped to the saved 1282x760 study.' }
$top = [Drawing.Rectangle]::new($profile.Top[0], $profile.Top[1], $profile.Top[2], $profile.Top[3])
$bottom = [Drawing.Rectangle]::new($profile.Bottom[0], $profile.Bottom[1], $profile.Bottom[2], $profile.Bottom[3])
$panelWidth = $top.Width
$panelHeight = $top.Height + $bottom.Height
[void][IO.Directory]::CreateDirectory($outputDirectory)

function Extract-Panel([Drawing.Bitmap]$source) {
    $normalized = $null
    $scale = $null
    try {
        if ($source.Width -ne $canonicalWidth -or $source.Height -ne $canonicalHeight) {
            if ($source.Width -ne $canonicalWidth -or $source.Height -ne $canonicalHeight - 1) { throw 'Unsupported source geometry.' }
            $normalized = [Drawing.Bitmap]::new($canonicalWidth, $canonicalHeight, $pixelFormat)
            $scale = [Drawing.Graphics]::FromImage($normalized)
            $scale.DrawImage($source, [Drawing.Rectangle]::new(0, 0, $canonicalWidth, $canonicalHeight))
        }
        $panel = [Drawing.Bitmap]::new($panelWidth, $panelHeight, $pixelFormat)
        $draw = [Drawing.Graphics]::FromImage($panel)
        try {
            $effective = if ($null -eq $normalized) { $source } else { $normalized }
            $draw.DrawImage($effective, [Drawing.Rectangle]::new(0, 0, $top.Width, $top.Height), $top, [Drawing.GraphicsUnit]::Pixel)
            $draw.DrawImage($effective, [Drawing.Rectangle]::new(0, $top.Height, $bottom.Width, $bottom.Height), $bottom, [Drawing.GraphicsUnit]::Pixel)
        }
        catch { $panel.Dispose(); throw }
        finally { $draw.Dispose() }
        return $panel
    }
    finally {
        if ($null -ne $scale) { $scale.Dispose() }
        if ($null -ne $normalized) { $normalized.Dispose() }
    }
}

$inputs = @(
    @{ Name = 'watch-neutral'; File = 'dance-60.png'; TimeMs = 60000; Kind = 'saved-full-frame' },
    @{ Name = 'right'; File = 'right-61_1.png'; TimeMs = 61100; Kind = 'saved-full-frame' },
    @{ Name = 'left'; File = 'calibration-frames/frame-0002.png'; TimeMs = 61600; Kind = 'saved-full-frame' },
    @{ Name = 'up'; File = 'up-62.png'; TimeMs = 62000; Kind = 'saved-full-frame' },
    @{ Name = 'down'; File = 'calibration-frames/frame-0005.png'; TimeMs = 68500; Kind = 'saved-full-frame' },
    @{ Name = 'success-feedback'; File = 'feedback-65.png'; TimeMs = 65000; Kind = 'saved-full-frame' },
    @{ Name = 'failure-feedback'; File = 'feedback-90.png'; TimeMs = 90000; Kind = 'saved-full-frame' },
    @{ Name = 'go-reference'; File = 'fade-study/Go.png'; TimeMs = $null; Kind = 'reference-reconstructed-full-frame' }
)
$rows = [Collections.Generic.List[object]]::new()
foreach ($entry in $inputs) {
    $sourcePath = Join-Path $studyDirectory $entry.File
    $loaded = [Drawing.Bitmap]::new($sourcePath)
    $source = $loaded
    try {
        if ($entry.Kind -eq 'reference-reconstructed-full-frame') {
            $source = [Drawing.Bitmap]::new($canonicalWidth, $canonicalHeight, $pixelFormat)
            $place = [Drawing.Graphics]::FromImage($source)
            try {
                $place.Clear([Drawing.Color]::Black)
                $place.DrawImage($loaded, $top, [Drawing.Rectangle]::new(0, 0, $top.Width, $top.Height), [Drawing.GraphicsUnit]::Pixel)
                $place.DrawImage($loaded, $bottom, [Drawing.Rectangle]::new(0, $top.Height, $bottom.Width, $bottom.Height), [Drawing.GraphicsUnit]::Pixel)
            }
            finally { $place.Dispose() }
        }
        if ($source.Width -ne $canonicalWidth -or $source.Height -ne $canonicalHeight) { throw 'Unexpected full-frame geometry.' }
        foreach ($variant in @('original', 'drop-bottom-normalize', 'drop-top-normalize', 'gdi-shrink-normalize')) {
            $candidate = $source
            try {
                if ($variant -eq 'drop-bottom-normalize') {
                    $candidate = $source.Clone([Drawing.Rectangle]::new(0, 0, $canonicalWidth, $canonicalHeight - 1), $pixelFormat)
                }
                elseif ($variant -eq 'drop-top-normalize') {
                    $candidate = $source.Clone([Drawing.Rectangle]::new(0, 1, $canonicalWidth, $canonicalHeight - 1), $pixelFormat)
                }
                elseif ($variant -eq 'gdi-shrink-normalize') {
                    $candidate = [Drawing.Bitmap]::new($canonicalWidth, $canonicalHeight - 1, $pixelFormat)
                    $shrink = [Drawing.Graphics]::FromImage($candidate)
                    try { $shrink.DrawImage($source, [Drawing.Rectangle]::new(0, 0, $canonicalWidth, $canonicalHeight - 1)) }
                    finally { $shrink.Dispose() }
                }
                $panel = Extract-Panel $candidate
                try {
                    $file = $entry.Name + '--' + $variant + '.png'
                    $path = Join-Path $outputDirectory $file
                    $panel.Save($path, [Drawing.Imaging.ImageFormat]::Png)
                    $rows.Add(@{
                        source = $sourcePath; source_sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash;
                        kind = $entry.Kind; name = $entry.Name; source_time_ms = $entry.TimeMs;
                        variant = $variant; panel = $file; panel_sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;
                        simulated_input_size = @($candidate.Width, $candidate.Height)
                    })
                }
                finally { $panel.Dispose() }
            }
            finally { if (-not [object]::ReferenceEquals($candidate, $source)) { $candidate.Dispose() } }
        }
    }
    finally {
        if (-not [object]::ReferenceEquals($source, $loaded)) { $source.Dispose() }
        $loaded.Dispose()
    }
}
@{
    schema_version = 1; mode = 'offline-gdi-geometry-diagnostic'; synthetic_variants = $true;
    profile = $profileFile; profile_sha256 = (Get-FileHash -LiteralPath $profileFile -Algorithm SHA256).Hash;
    drawing_assembly = [Drawing.Graphics].Assembly.FullName; graphics_settings = 'Default GDI+ DrawImage settings, matching PetDancePanelVision.Extract';
    source_video_sha256 = $profile.SourceVideoSha256; input_count = $inputs.Count; panels = $rows;
    limits = 'Saved frames plus synthetic geometry transforms, not actual current-game footage. Go full frame is reconstructed from its frozen panel. No gameplay, input or live accuracy claim.'
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputDirectory 'gdi-panels.json') -Encoding utf8
Write-Output "Created $($rows.Count) GDI+ panels from $($inputs.Count) bounded inputs."
