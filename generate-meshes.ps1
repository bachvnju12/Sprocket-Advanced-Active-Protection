# Generates low-poly placeholder OBJ meshes for all APS modules.
# These are original low-poly placeholder meshes (GPL-3.0-only).
# Replace with licensed models before distribution.
param(
    [string]$OutputDir = "D:\workspace\sporketmod\SAAPS\Assets\Models"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

function Write-Obj {
    param(
        [string]$Path,
        [string]$Name,
        [string[]]$Vertices,
        [string[]]$Faces,
        [string]$Material = "saaps_dark_metal"
    )
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# $Name - low-poly placeholder (GPL-3.0-only)")
    [void]$sb.AppendLine("o $Name")
    [void]$sb.AppendLine("mtllib saaps.mtl")
    [void]$sb.AppendLine("usemtl $Material")
    foreach ($v in $Vertices) {
        [void]$sb.AppendLine("v $v")
    }
    [void]$sb.AppendLine("vn 0.0 1.0 0.0")
    foreach ($f in $Faces) {
        [void]$sb.AppendLine("f $f")
    }
    Set-Content -Path $Path -Value $sb.ToString() -Encoding UTF8
    Write-Host "Generated $([System.IO.Path]::GetFileName($Path))"
}

# Helper: box mesh centered at origin with given half-extents
function New-Box {
    param([float]$hx, [float]$hy, [float]$hz)
    $verts = @(
        "$(-$hx) $(-$hy) $(-$hz)",
        "$($hx) $(-$hy) $(-$hz)",
        "$($hx) $(-$hy) $($hz)",
        "$(-$hx) $(-$hy) $($hz)",
        "$(-$hx) $($hy) $(-$hz)",
        "$($hx) $($hy) $(-$hz)",
        "$($hx) $($hy) $($hz)",
        "$(-$hx) $($hy) $($hz)"
    )
    $faces = @(
        "1 2 3 4",   # bottom
        "5 8 7 6",   # top
        "1 5 6 2",   # front
        "2 6 7 3",   # right
        "3 7 8 4",   # back
        "4 8 5 1"    # left
    )
    return @($verts, $faces)
}

# Helper: cylinder mesh (approximated with 8 sides)
function New-Cylinder {
    param([float]$r, [float]$h, [int]$sides = 8)
    $verts = @()
    $faces = @()
    $halfH = $h / 2
    # Bottom center
    $verts += "0.0 $(-$halfH) 0.0"
    # Top center
    $verts += "0.0 $($halfH) 0.0"
    # Bottom ring
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 2 * [Math]::PI * $i / $sides
        $x = $r * [Math]::Cos($a)
        $z = $r * [Math]::Sin($a)
        $verts += "$($x.ToString('0.0000')) $(-$halfH) $($z.ToString('0.0000'))"
    }
    # Top ring
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 2 * [Math]::PI * $i / $sides
        $x = $r * [Math]::Cos($a)
        $z = $r * [Math]::Sin($a)
        $verts += "$($x.ToString('0.0000')) $($halfH) $($z.ToString('0.0000'))"
    }
    # Bottom cap (fan from vertex 1)
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 3 + $i
        $b = 3 + (($i + 1) % $sides)
        $faces += "1 $a $b"
    }
    # Top cap (fan from vertex 2)
    $topStart = 3 + $sides
    for ($i = 0; $i -lt $sides; $i++) {
        $a = $topStart + $i
        $b = $topStart + (($i + 1) % $sides)
        $faces += "2 $b $a"
    }
    # Side quads
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 3 + $i
        $b = 3 + (($i + 1) % $sides)
        $c = $topStart + (($i + 1) % $sides)
        $d = $topStart + $i
        $faces += "$a $b $c $d"
    }
    return @($verts, $faces)
}

# Helper: radar dish (parabolic approximation with a cone + disc)
function New-RadarDish {
    param([float]$r, [float]$depth, [int]$sides = 12)
    $verts = @()
    $faces = @()
    # Center vertex (back of dish)
    $verts += "0.0 0.0 0.0"
    # Rim vertices
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 2 * [Math]::PI * $i / $sides
        $x = $r * [Math]::Cos($a)
        $y = $r * [Math]::Sin($a)
        $verts += "$($x.ToString('0.0000')) $($y.ToString('0.0000')) $($depth.ToString('0.0000'))"
    }
    # Dish surface (fan)
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 2 + $i
        $b = 2 + (($i + 1) % $sides)
        $faces += "1 $a $b"
    }
    # Rim (side ring)
    for ($i = 0; $i -lt $sides; $i++) {
        $a = 2 + $i
        $b = 2 + (($i + 1) % $sides)
        $faces += "$a $b $b $a"  # degenerate, just for visibility
    }
    return @($verts, $faces)
}

# Generate meshes for each APS system
$systems = @(
    @{ Id = "drozd1";   Name = "Drozd-1";        Prefix = "0001" },
    @{ Id = "drozd2";   Name = "Drozd-2";        Prefix = "0002" },
    @{ Id = "arena";    Name = "Arena";              Prefix = "0003" },
    @{ Id = "arenam";   Name = "Arena-M";        Prefix = "0004" },
    @{ Id = "afganit";  Name = "Afganit";        Prefix = "0005" },
    @{ Id = "shtora1";  Name = "Shtora-1";       Prefix = "0006" },
    @{ Id = "trophy";   Name = "Trophy";             Prefix = "0007" },
    @{ Id = "ironfist"; Name = "Iron Fist";      Prefix = "0008" },
    @{ Id = "amapads";  Name = "AMAP-ADS";       Prefix = "0009" },
    @{ Id = "zaslin";   Name = "Zaslin";          Prefix = "0010" }
)

foreach ($sys in $systems) {
    $id = $sys.Id

    # RADAR: box base + cylinder mast + radar dish
    $radarPath = Join-Path $OutputDir "$($id)_radar.obj"
    $box = New-Box 0.15 0.05 0.10
    $mast = New-Cylinder 0.02 0.10
    $dish = New-RadarDish 0.12 0.04
    $verts = $box[0] + $mast[0] + $dish[0]
    $faces = $box[1] + $mast[1] + $dish[1]
    Write-Obj -Path $radarPath -Name "$($sys.Name) Radar" -Vertices $verts -Faces $faces

    # LAUNCHER: box base + multiple cylinder tubes
    $launchPath = Join-Path $OutputDir "$($id)_launcher.obj"
    $lbox = New-Box 0.20 0.08 0.12
    $tube1 = New-Cylinder 0.03 0.20
    $tube2 = New-Cylinder 0.03 0.20
    $tube3 = New-Cylinder 0.03 0.20
    $tube4 = New-Cylinder 0.03 0.20
    $lverts = $lbox[0] + $tube1[0] + $tube2[0] + $tube3[0] + $tube4[0]
    $lfaces = $lbox[1] + $tube1[1] + $tube2[1] + $tube3[1] + $tube4[1]
    Write-Obj -Path $launchPath -Name "$($sys.Name) Launcher" -Vertices $lverts -Faces $lfaces

    # SENSOR: small box + cylinder lens
    $sensorPath = Join-Path $OutputDir "$($id)_sensor.obj"
    $sbox = New-Box 0.08 0.06 0.06
    $lens = New-Cylinder 0.025 0.02
    $sverts = $sbox[0] + $lens[0]
    $sfaces = $sbox[1] + $lens[1]
    Write-Obj -Path $sensorPath -Name "$($sys.Name) Sensor" -Vertices $sverts -Faces $sfaces
}

# Material library
$mtl = @"
# SAAPS materials - low-poly placeholder (GPL-3.0-only)
newmtl saaps_dark_metal
Ka 0.10 0.10 0.11
Kd 0.25 0.26 0.28
Ks 0.40 0.40 0.42
Ns 32.0
d 1.0

newmtl saaps_light_metal
Ka 0.15 0.15 0.15
Kd 0.45 0.46 0.48
Ks 0.50 0.50 0.52
Ns 48.0
d 1.0

newmtl saaps_lens
Ka 0.02 0.03 0.04
Kd 0.05 0.10 0.12
Ks 0.80 0.85 0.90
Ns 96.0
d 1.0

newmtl saaps_radar
Ka 0.08 0.08 0.09
Kd 0.20 0.21 0.23
Ks 0.30 0.30 0.32
Ns 24.0
d 1.0
"@
Set-Content -Path (Join-Path $OutputDir "saaps.mtl") -Value $mtl -Encoding UTF8
Write-Host "Generated saaps.mtl"

Write-Host "Done. Generated $($systems.Count * 3) mesh files."
