# Generates simple SVG icons for all APS parts.
# These are original placeholder icons (GPL-3.0-only).
param(
    [string]$OutputDir = "D:\workspace\sporketmod\SAAPS\Assets\Icons"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$systems = @(
    @{ Id = "drozd1";   Name = "Drozd-1";        Color = "#4a5d23" },
    @{ Id = "drozd2";   Name = "Drozd-2";        Color = "#3d4f1f" },
    @{ Id = "arena";    Name = "Arena";              Color = "#5d4037" },
    @{ Id = "arenam";   Name = "Arena-M";        Color = "#4e342e" },
    @{ Id = "afganit";  Name = "Afganit";        Color = "#1a237e" },
    @{ Id = "shtora1";  Name = "Shtora-1";       Color = "#7b1fa2" },
    @{ Id = "trophy";   Name = "Trophy";             Color = "#b71c1c" },
    @{ Id = "ironfist"; Name = "Iron Fist";      Color = "#e65100" },
    @{ Id = "amapads";  Name = "AMAP-ADS";       Color = "#37474f" },
    @{ Id = "zaslin";   Name = "Zaslin";          Color = "#00695c" }
)

$components = @("Radar", "Launcher", "Sensor")

foreach ($sys in $systems) {
    foreach ($comp in $components) {
        $iconName = "$($sys.Id)$($comp)"
        $svgPath = Join-Path $OutputDir "$iconName.svg"

        # Distinct icon shapes per component type
        $shape = switch ($comp) {
            "Radar" {
                # Radar: circle with sweeping line
                @"
<circle cx="32" cy="32" r="20" fill="none" stroke="$($sys.Color)" stroke-width="3"/>
<circle cx="32" cy="32" r="12" fill="none" stroke="$($sys.Color)" stroke-width="2" opacity="0.5"/>
<line x1="32" y1="32" x2="48" y2="20" stroke="$($sys.Color)" stroke-width="2"/>
<circle cx="32" cy="32" r="3" fill="$($sys.Color)"/>
"@
            }
            "Launcher" {
                # Launcher: multiple tubes
                @"
<rect x="14" y="18" width="8" height="28" rx="2" fill="$($sys.Color)"/>
<rect x="24" y="14" width="8" height="32" rx="2" fill="$($sys.Color)"/>
<rect x="34" y="14" width="8" height="32" rx="2" fill="$($sys.Color)"/>
<rect x="44" y="18" width="8" height="28" rx="2" fill="$($sys.Color)"/>
<rect x="12" y="44" width="42" height="6" rx="2" fill="$($sys.Color)" opacity="0.6"/>
"@
            }
            "Sensor" {
                # Sensor: eye/lens shape
                @"
<ellipse cx="32" cy="32" rx="22" ry="14" fill="none" stroke="$($sys.Color)" stroke-width="3"/>
<circle cx="32" cy="32" r="8" fill="$($sys.Color)" opacity="0.7"/>
<circle cx="32" cy="32" r="4" fill="#ffffff" opacity="0.5"/>
"@
            }
        }

        $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <rect width="64" height="64" rx="8" fill="#1e1e1e"/>
  $shape
  <text x="32" y="58" font-family="sans-serif" font-size="6" fill="#888888" text-anchor="middle">$($sys.Name) $comp</text>
</svg>
"@
        Set-Content -Path $svgPath -Value $svg -Encoding UTF8
        Write-Host "Generated $iconName.svg"
    }
}

# Category icon
$catSvg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <rect width="64" height="64" rx="8" fill="#1e1e1e"/>
  <circle cx="24" cy="24" r="10" fill="none" stroke="#4caf50" stroke-width="3"/>
  <line x1="24" y1="24" x2="36" y2="16" stroke="#4caf50" stroke-width="2"/>
  <rect x="38" y="30" width="6" height="20" rx="1" fill="#4caf50"/>
  <rect x="46" y="30" width="6" height="20" rx="1" fill="#4caf50"/>
  <text x="32" y="58" font-family="sans-serif" font-size="6" fill="#888888" text-anchor="middle">APS</text>
</svg>
"@
Set-Content -Path (Join-Path $OutputDir "aps_category.svg") -Value $catSvg -Encoding UTF8
Write-Host "Generated aps_category.svg"

Write-Host "Done. Generated $($systems.Count * $components.Count + 1) icon files."
