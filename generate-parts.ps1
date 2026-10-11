param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot "generated")
)

$ErrorActionPreference = "Stop"
$partsDir = Join-Path $OutputRoot "Sprocket_Data\StreamingAssets\Parts"
$localizationDir = Join-Path $OutputRoot "Sprocket_Data\StreamingAssets\Localization\en-UK\Parts"
$meshesDir = Join-Path $OutputRoot "meshes"
$iconsDir = Join-Path $OutputRoot "icons"
$previewsDir = Join-Path $OutputRoot "previews"
$texturesDir = Join-Path $OutputRoot "textures"
$atlasPath = Join-Path $texturesDir "saaps_surfaces.png"
New-Item -ItemType Directory -Force -Path $partsDir, $localizationDir, $meshesDir, $iconsDir, $previewsDir, $texturesDir | Out-Null
Add-Type -AssemblyName System.Drawing

$systems = @(
    @{ Id = "drozd1"; Name = "Drozd-1"; Country = "USSR"; Year = 1982; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000001"; Shape = "drozd1" },
    @{ Id = "drozd2"; Name = "Drozd-2"; Country = "Russia"; Year = 1995; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000002"; Shape = "drozd2" },
    @{ Id = "arena"; Name = "Arena"; Country = "Russia"; Year = 1996; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000003"; Shape = "arena" },
    @{ Id = "arenam"; Name = "Arena-M"; Country = "Russia"; Year = 2018; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000004"; Shape = "arenam" },
    @{ Id = "afganit"; Name = "Afganit"; Country = "Russia"; Year = 2015; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000005"; Shape = "afganit" },
    @{ Id = "shtora1"; Name = "Shtora-1"; Country = "USSR"; Year = 1988; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000006"; Shape = "shtora1" },
    @{ Id = "trophy"; Name = "Trophy (Windbreaker)"; Country = "Israel"; Year = 2009; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000007"; Shape = "trophy" },
    @{ Id = "ironfist"; Name = "Iron Fist"; Country = "Israel"; Year = 2010; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000008"; Shape = "ironfist" },
    @{ Id = "amapads"; Name = "AMAP-ADS (StrikeShield)"; Country = "Germany"; Year = 2007; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000009"; Shape = "amapads" },
    @{ Id = "zaslin"; Name = "Zaslin"; Country = "Ukraine"; Year = 2016; Guid = "a91bb31a-2cf1-4d0a-8e31-000000000010"; Shape = "zaslin" }
)

function New-BoxMesh([double]$X, [double]$Z, [double]$Height) {
    $verts = @(-$X,0,-$Z, $X,0,-$Z, $X,0,$Z, -$X,0,$Z, -$X,$Height,-$Z, $X,$Height,-$Z, $X,$Height,$Z, -$X,$Height,$Z)
    $faces = @(
        @{ v = @(0,3,2,1); t = @(5,5,5,5) }, @{ v = @(4,5,6,7); t = @(5,5,5,5) },
        @{ v = @(0,1,5,4); t = @(5,5,5,5) }, @{ v = @(1,2,6,5); t = @(5,5,5,5) },
        @{ v = @(2,3,7,6); t = @(5,5,5,5) }, @{ v = @(3,0,4,7); t = @(5,5,5,5) }
    )
    return @{ vertices = $verts; faces = $faces }
}

function Add-Box([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$X, [double]$Y, [double]$Z, [double]$Width, [double]$Height, [double]$Depth, [int]$Surface = 0) {
    $mesh = New-BoxMesh ($Width / 2) ($Depth / 2) $Height
    $base = $Vertices.Count / 3
    for ($i = 0; $i -lt $mesh.vertices.Count; $i += 3) {
        $Vertices.Add($X + $mesh.vertices[$i])
        $Vertices.Add($Y + $mesh.vertices[$i + 1])
        $Vertices.Add($Z + $mesh.vertices[$i + 2])
    }
    foreach ($face in $mesh.faces) {
        $Faces.Add(@{ v = @($face.v | ForEach-Object { $_ + $base }); t = $face.t; surface = $Surface })
    }
}

# Faceted drum, upright on Y. Quads only, same winding and material slot as New-BoxMesh,
# so the game mesh loader handles it exactly as it does the box parts.
function Add-Drum([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$X, [double]$Y, [double]$Z, [double]$Diameter, [double]$Height, [int]$Sides = 8) {
    if ($Diameter -gt 0.045 -and $Sides -lt 32) { $Sides = 32 }
    if ($Sides -lt 6) { $Sides = 6 }
    if (($Sides % 2) -ne 0) { $Sides++ }
    $radius = $Diameter / 2
    $base = $Vertices.Count / 3
    foreach ($ring in 0, 1) {
        for ($i = 0; $i -lt $Sides; $i++) {
            $angle = 2 * [Math]::PI * $i / $Sides
            $Vertices.Add($X + $radius * [Math]::Cos($angle))
            $Vertices.Add($Y + $Height * $ring)
            $Vertices.Add($Z + $radius * [Math]::Sin($angle))
        }
    }
    for ($i = 0; $i -lt $Sides; $i++) {
        $next = ($i + 1) % $Sides
        # Barrel wall. Winding matches the box faces so the mesh loader needs no special case.
        $Faces.Add(@{ v = @(($base + $i), ($base + $next), ($base + $Sides + $next), ($base + $Sides + $i)); t = @(5, 5, 5, 5); smooth = "$base/drum";
            uv = @(($i / $Sides), 0, (($i + 1) / $Sides), 0, (($i + 1) / $Sides), 1, ($i / $Sides), 1) })
    }
    # Flat caps as single n-gons; winding mirrors the box top/bottom faces. Texture slot
    # count tracks the vertex count, matching the quad faces the game ships.
    $top = @(); $bottom = @()
    for ($i = 0; $i -lt $Sides; $i++) {
        $top += ($base + $Sides + $i)
        $bottom += ($base + (($Sides - $i) % $Sides))
    }
    $Faces.Add(@{ v = $top; t = @(5) * $Sides })
    $Faces.Add(@{ v = $bottom; t = @(5) * $Sides })
}

# Box pivoting on its own base, pitched about X then yawed about Y. Both are proper
# rotations, so the outward winding from New-BoxMesh survives and the loader is unchanged.
function Add-RotatedBox([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$X, [double]$Y, [double]$Z, [double]$Width, [double]$Height, [double]$Depth, [double]$Pitch, [double]$Yaw) {
    $mesh = New-BoxMesh ($Width / 2) ($Depth / 2) $Height
    $cosP = [Math]::Cos($Pitch); $sinP = [Math]::Sin($Pitch)
    $cosY = [Math]::Cos($Yaw); $sinY = [Math]::Sin($Yaw)
    $base = $Vertices.Count / 3
    for ($v = 0; $v -lt 8; $v++) {
        $px = $mesh.vertices[$v * 3]; $py = $mesh.vertices[$v * 3 + 1]; $pz = $mesh.vertices[$v * 3 + 2]
        # Pitch about X, then yaw about Y.
        $ry = $py * $cosP - $pz * $sinP
        $rz = $py * $sinP + $pz * $cosP
        $Vertices.Add($X + $px * $cosY + $rz * $sinY)
        $Vertices.Add($Y + $ry)
        $Vertices.Add($Z + (-$px * $sinY + $rz * $cosY))
    }
    foreach ($face in $mesh.faces) {
        $Faces.Add(@{ v = @($face.v | ForEach-Object { $_ + $base }); t = $face.t })
    }
}

# Ring of launch tubes leaning outward from the turret, the signature of the Soviet ring
# systems. Tilt is degrees from vertical.
function Add-LauncherRing([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$Radius, [double]$Y, [int]$Count, [double]$Tilt, [double]$Diameter, [double]$Height) {
    $radians = $Tilt * [Math]::PI / 180
    for ($i = 0; $i -lt $Count; $i++) {
        $a = 2 * [Math]::PI * $i / $Count + [Math]::PI / 4
        # Pitch leans the tube toward +Z; this yaw maps +Z onto the ring point.
        Add-RotatedBox $Vertices $Faces ($Radius * [Math]::Cos($a)) $Y ($Radius * [Math]::Sin($a)) $Diameter $Height $Diameter $radians ([Math]::PI / 2 - $a)
    }
}

# Horizontal capped tube, axis along local +Z and yawed into place. The round emitter head
# that sits on each turret cheek of the Shtora-1 and Afganit dazzlers.
function Add-Tube([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$X, [double]$Y, [double]$Z, [double]$Diameter, [double]$Depth, [int]$Sides, [double]$Yaw, [double]$Pitch = 0, [double]$Recess = 0, [int]$Surface = 0) {
    if ($Diameter -gt 0.045 -and $Sides -lt 40) { $Sides = 40 }
    if ($Sides -lt 6) { $Sides = 6 }
    if ($Diameter -le 0 -or $Depth -le 0 -or $Recess -lt 0 -or $Recess -ge $Depth) { throw "Invalid tube dimensions" }
    $radius = $Diameter / 2
    $cosY = [Math]::Cos($Yaw); $sinY = [Math]::Sin($Yaw)
    $cosP = [Math]::Cos($Pitch); $sinP = [Math]::Sin($Pitch)
    $base = $Vertices.Count / 3
    $radii = @($radius, $radius); $offsets = @(0, $Depth)
    if ($Recess -gt 0) {
        $radii += @(($radius * 0.78), ($radius * 0.78))
        $offsets += @($Depth, ($Depth - $Recess))
    }
    for ($ring = 0; $ring -lt $radii.Count; $ring++) {
        for ($i = 0; $i -lt $Sides; $i++) {
            $angle = 2 * [Math]::PI * $i / $Sides
            $px = $radii[$ring] * [Math]::Cos($angle)
            $py = -$radii[$ring] * [Math]::Sin($angle)
            $ry = $py * $cosP - $offsets[$ring] * $sinP
            $rz = $py * $sinP + $offsets[$ring] * $cosP
            $Vertices.Add($X + $px * $cosY + $rz * $sinY)
            $Vertices.Add($Y + $ry)
            $Vertices.Add($Z - $px * $sinY + $rz * $cosY)
        }
    }
    # Outer wall, front annulus and recessed inner wall; no concave cap fan.
    for ($ring = 0; $ring -lt $radii.Count - 1; $ring++) {
        for ($i = 0; $i -lt $Sides; $i++) {
            $next = ($i + 1) % $Sides
            $a = $base + $ring * $Sides; $b = $a + $Sides
            $face = @{ v = @(($a + $i), ($a + $next), ($b + $next), ($b + $i)); t = @(5, 5, 5, 5); surface = $Surface }
            if ($ring -ne 1) {
                $face.smooth = "$base/$ring"
                $face.uv = @(($i / $Sides), 0, (($i + 1) / $Sides), 0, (($i + 1) / $Sides), 1, ($i / $Sides), 1)
                if ($ring -gt 1) { $face.surface = 1 }
            }
            $Faces.Add($face)
        }
    }
    $near = @(); $far = @()
    for ($i = 0; $i -lt $Sides; $i++) {
        $near += ($base + (($Sides - $i) % $Sides))
        $far += ($base + ($radii.Count - 1) * $Sides + $i)
    }
    $Faces.Add(@{ v = $near; t = @(5) * $Sides; surface = $Surface })
    $Faces.Add(@{ v = $far; t = @(5) * $Sides; surface = $(if ($Recess -gt 0 -and $Surface -eq 0) { 1 } else { $Surface }) })
}

# Convex clipped rectangle extruded along +Z. Y is its lower edge, not its centre.
function Add-ChamferBox($Vertices, $Faces, [double]$X, [double]$Y, [double]$Z, [double]$Width, [double]$Height, [double]$Depth, [double]$Bevel, [int]$Surface = 0) {
    if ($Bevel -le 0 -or $Bevel -ge [Math]::Min($Width, $Height) / 2 -or $Depth -le 0) { throw "Invalid housing bevel" }
    $w = $Width / 2
    $profile = @(-$w, $Bevel, -$w, ($Height - $Bevel), (-$w + $Bevel), $Height,
        ($w - $Bevel), $Height, $w, ($Height - $Bevel), $w, $Bevel, ($w - $Bevel), 0, (-$w + $Bevel), 0)
    $base = $Vertices.Count / 3
    $edge = [Math]::Min($Depth * 0.18, $Bevel * 0.24)
    # Two bevel steps on each end catch light without rounding the whole housing.
    $offsets = @(0, ($edge * 0.3), $edge, ($Depth - $edge), ($Depth - $edge * 0.3), $Depth)
    $insets = @($edge, ($edge * 0.3), 0, 0, ($edge * 0.3), $edge)
    for ($ring = 0; $ring -lt 6; $ring++) {
        for ($i = 0; $i -lt 8; $i++) {
            $Vertices.Add($X + $profile[2 * $i] * (1 - 2 * $insets[$ring] / $Width))
            $Vertices.Add($Y + $Height / 2 + ($profile[2 * $i + 1] - $Height / 2) * (1 - 2 * $insets[$ring] / $Height))
            $Vertices.Add($Z + $offsets[$ring])
        }
    }
    for ($ring = 0; $ring -lt 5; $ring++) {
        for ($i = 0; $i -lt 8; $i++) {
            $next = ($i + 1) % 8; $a = $base + $ring * 8; $b = $a + 8
            $Faces.Add(@{ v = @(($a + $i), ($a + $next), ($b + $next), ($b + $i)); t = @(5, 5, 5, 5); surface = $Surface })
        }
    }
    $Faces.Add(@{ v = @(7..0 | ForEach-Object { $base + $_ }); t = @(5) * 8; surface = $Surface })
    $Faces.Add(@{ v = @(0..7 | ForEach-Object { $base + 40 + $_ }); t = @(5) * 8; surface = $Surface })
}

# Block of launch tubes in stacked rows, all leaning the same way. Tilt is degrees from
# vertical and Yaw points the lean; a yaw of PI / 2 leans toward +X, PI toward -Z.
function Add-TubeCluster([System.Collections.Generic.List[double]]$Vertices, [System.Collections.Generic.List[object]]$Faces, [double]$X, [double]$Y, [double]$Z, [int]$Columns, [int]$Rows, [double]$Gap, [double]$Tilt, [double]$Yaw, [double]$Diameter, [double]$Height) {
    $cosY = [Math]::Cos($Yaw); $sinY = [Math]::Sin($Yaw)
    for ($row = 0; $row -lt $Rows; $row++) {
        for ($column = 0; $column -lt $Columns; $column++) {
            # Spacing runs across the lean, horizontally, so the block reads as a grid.
            $side = ($column - ($Columns - 1) / 2) * $Gap
            Add-Tube $Vertices $Faces ($X + $side * $cosY) ($Y + $row * $Gap * 1.1) ($Z - $side * $sinY) `
                $Diameter $Height 20 $Yaw (-[Math]::PI / 2 + $Tilt * [Math]::PI / 180) ($Height * 0.08)
        }
    }
}

# Seven photo-referenced exterior assemblies; Drozd-2, Arena-M and Zaslin remain placeholders.
# Dimensions are estimates, not measured hardware specifications.
function New-SystemMesh($Shape) {
    $vertices = [System.Collections.Generic.List[double]]::new()
    $faces = [System.Collections.Generic.List[object]]::new()
    switch ($Shape) {
        "custom" { Add-ChamferBox $vertices $faces 0 0 -0.10 0.25 0.15 0.20 0.01 }
        # One cheek module from the T-55AD photo: four cylindrical canisters in paired
        # banks on a mounting cradle. +Z is the exposed front; no invented turret base.
        "drozd1" {
            Add-ChamferBox $vertices $faces 0 0 -0.035 0.19 0.22 0.055 0.012
            foreach ($x in -0.043, 0.043) {
                foreach ($y in 0.065, 0.15) {
                    Add-Tube $vertices $faces $x $y 0.006 0.078 0.235 24 0 (-0.08) 0.012
                    Add-Tube $vertices $faces $x ($y + 0.001) 0.02 0.086 0.018 24 0 (-0.08)
                    Add-Tube $vertices $faces $x ($y + 0.009) 0.13 0.082 0.014 24 0 (-0.08)
                }
            }
            # Upper radar housing has a sloping roof and overhanging lip in T-55AD photo.
            Add-ChamferBox $vertices $faces 0 0.205 -0.035 0.196 0.052 0.18 0.01
            Add-RotatedBox $vertices $faces 0 0.254 0.055 0.208 0.017 0.20 (-0.12) 0
            foreach ($x in -0.043, 0.043) {
                Add-Tube $vertices $faces $x 0.15 0.237 0.049 0.012 32 0 (-0.08) 0.004 1
            }
            foreach ($x in -0.078, 0.078) {
                Add-Box $vertices $faces $x 0 -0.02 0.025 0.026 0.17
            }
        }
        # Placeholder: no verified Drozd-2 variant photo; not a hardware reconstruction.
        "drozd2" {
            Add-Drum $vertices $faces 0 0.03 0 0.27 0.06 12
            Add-TubeCluster $vertices $faces 0.085 0.075 0 3 2 0.055 34 ([Math]::PI / 2) 0.05 0.12
            Add-TubeCluster $vertices $faces -0.085 0.075 0 3 2 0.055 34 (-[Math]::PI / 2) 0.05 0.12
            Add-Drum $vertices $faces 0 0.09 -0.02 0.15 0.06 10
            Add-Box $vertices $faces 0 0.08 -0.105 0.17 0.10 0.06
        }
        # T-80UM-1 roof radar mast only; do not miniaturize the whole turret installation.
        "arena" {
            Add-Box $vertices $faces 0 0 0 0.155 0.025 0.15
            foreach ($x in -0.051, 0.051) { Add-Box $vertices $faces $x 0.025 0 0.022 0.087 0.10 1 }
            Add-Drum $vertices $faces 0 0.106 0 0.144 0.037 32
            Add-ChamferBox $vertices $faces 0 0.139 -0.068 0.166 0.18 0.136 0.026
            foreach ($x in -0.076, 0.076) {
                Add-Box $vertices $faces $x 0.139 0.002 0.024 0.18 0.018
            }
            Add-Box $vertices $faces 0 0.319 0 0.176 0.016 0.147
        }
        # Placeholder: no verified Arena-M variant photo; legacy panel-ring layout.
        "arenam" {
            Add-Drum $vertices $faces 0 0.02 0 0.27 0.08 12
            Add-LauncherRing $vertices $faces 0.152 0.045 16 6 0.04 0.065
            Add-ChamferBox $vertices $faces 0 0.10 -0.09 0.20 0.09 0.18 0.015
            Add-ChamferBox $vertices $faces 0 0.114 0.091 0.158 0.063 0.006 0.009 2
            foreach ($x in -0.072, 0.072) {
                Add-Box $vertices $faces $x 0.1 -0.008 0.016 0.08 0.19 1
                Add-Tube $vertices $faces $x 0.184 0.016 0.01 0.009 6 0 0 0 1
            }
            Add-Drum $vertices $faces 0 0.19 0 0.09 0.05 8
            Add-RotatedBox $vertices $faces 0 0.10 -0.07 0.16 0.028 0.11 (30 * [Math]::PI / 180) ([Math]::PI)
        }
        # T-14 exterior photo: one low bank of five capped, splayed hard-kill canisters.
        # No Shtora emitter or fictitious radar disc; hidden internals omitted.
        "afganit" {
            Add-ChamferBox $vertices $faces 0 0 -0.10 0.43 0.058 0.18 0.012
            for ($i = 0; $i -lt 5; $i++) {
                $x = ($i - 2) * 0.083; $yaw = ($i - 2) * 0.11
                Add-Tube $vertices $faces $x 0.083 -0.083 0.072 0.245 40 $yaw 0
                Add-Tube $vertices $faces $x 0.083 -0.073 0.078 0.019 40 $yaw 0
                Add-Tube $vertices $faces ($x + [Math]::Sin($yaw) * 0.226) 0.083 (-0.083 + [Math]::Cos($yaw) * 0.226) 0.074 0.019 40 $yaw 0
                Add-Box $vertices $faces $x 0.011 -0.04 0.018 0.028 0.14 1
            }
            Add-Box $vertices $faces 0 0.111 -0.11 0.43 0.018 0.06
        }
        # Single T-90 emitter housing: circular front, finned body, protective handle,
        # and low mounting feet. Geometry details are selective, not every cooling pin.
        "shtora1" {
            Add-Box $vertices $faces -0.105 0 0 0.07 0.035 0.19
            Add-Box $vertices $faces 0.105 0 0 0.07 0.035 0.19
            Add-ChamferBox $vertices $faces 0 0.035 -0.10 0.25 0.235 0.175 0.027
            Add-ChamferBox $vertices $faces 0 0.037 0.075 0.255 0.232 0.019 0.034
            # Reference shows the painted protective cover, not an exposed red/black lens.
            Add-Tube $vertices $faces 0 0.154 0.093 0.205 0.016 48 0
            foreach ($sign in -1, 1) {
                $x = $sign * 0.133
                for ($i = 0; $i -lt 12; $i++) {
                    Add-ChamferBox $vertices $faces $x (0.053 + $i * 0.017) 0.046 0.043 0.006 0.064 0.002
                }
                Add-Box $vertices $faces $x 0.032 -0.075 0.026 0.272 0.022
                for ($row = 0; $row -lt 6; $row++) {
                    for ($column = 0; $column -lt 5; $column++) {
                        Add-Tube $vertices $faces ($sign * 0.125) (0.068 + $row * 0.029) (-0.065 + $column * 0.024) 0.007 0.014 8 ($sign * [Math]::PI / 2)
                    }
                }
            }
            for ($row = 0; $row -lt 5; $row++) {
                for ($column = 0; $column -lt 8; $column++) {
                    Add-Drum $vertices $faces (-0.091 + $column * 0.026) 0.27 (-0.066 + $row * 0.027) 0.007 0.017 8
                }
            }
            # Swept protective bow and cheek pivots visible in the close-up.
            $bow = @(@(-0.15,0.10), @(-0.15,0.27), @(-0.13,0.315), @(-0.095,0.33), @(0.095,0.33), @(0.13,0.315), @(0.15,0.27), @(0.15,0.10))
            for ($i = 0; $i -lt $bow.Count - 1; $i++) {
                $dx = $bow[$i + 1][0] - $bow[$i][0]; $dy = $bow[$i + 1][1] - $bow[$i][1]
                $length = [Math]::Sqrt($dx * $dx + $dy * $dy)
                Add-Tube $vertices $faces $bow[$i][0] $bow[$i][1] -0.077 0.025 $length 12 ([Math]::PI / 2) (-[Math]::Atan2($dy, [Math]::Abs($dx)))
            }
            foreach ($sign in -1, 1) {
                Add-Tube $vertices $faces ($sign * 0.134) 0.074 -0.077 0.063 0.026 32 ($sign * [Math]::PI / 2)
                Add-Tube $vertices $faces ($sign * 0.160) 0.074 -0.077 0.034 0.007 24 ($sign * [Math]::PI / 2)
            }
            foreach ($x in -0.106, 0.106) {
                foreach ($y in 0.067, 0.238) {
                    Add-Tube $vertices $faces $x $y 0.094 0.014 0.007 12 0
                }
            }
        }
        # Merkava side installation: clipped radar face below a shielded launcher.
        # Separate mounting blocks replace the fictitious horizontal radar disc.
        "trophy" {
            Add-Box $vertices $faces -0.10 0 -0.03 0.05 0.16 0.075
            Add-Box $vertices $faces 0.10 0 -0.03 0.05 0.16 0.075
            Add-ChamferBox $vertices $faces 0 0.025 0 0.34 0.15 0.042 0.036
            Add-ChamferBox $vertices $faces 0 0.033 0.043 0.322 0.134 0.008 0.029
            Add-Box $vertices $faces 0.082 0.16 -0.058 0.075 0.045 0.115
            Add-Drum $vertices $faces 0.082 0.205 -0.058 0.073 0.025 24
            Add-ChamferBox $vertices $faces 0.082 0.23 -0.079 0.091 0.15 0.05 0.008
            Add-RotatedBox $vertices $faces -0.018 0.20 -0.083 0.012 0.215 0.115 0 (-0.22)
            Add-RotatedBox $vertices $faces 0.18 0.20 -0.083 0.012 0.215 0.115 0 0.22
            Add-Box $vertices $faces 0.082 0.396 -0.083 0.212 0.019 0.115
            Add-Box $vertices $faces 0.082 0.20 -0.13 0.212 0.017 0.115
            foreach ($x in -0.139, 0.139) {
                Add-Tube $vertices $faces $x 0.117 0.051 0.012 0.005 12 0
            }
        }
        # Iron Fist demonstration unit: two vertically stacked tubes on a rotary yoke.
        "ironfist" {
            Add-Drum $vertices $faces 0 0 0 0.21 0.024 40
            foreach ($x in -0.074, 0.074) { Add-Box $vertices $faces $x 0.024 0 0.016 0.053 0.15 1 }
            Add-Drum $vertices $faces 0 0.075 0 0.22 0.017 40
            Add-ChamferBox $vertices $faces 0 0.092 -0.091 0.124 0.222 0.133 0.029
            foreach ($y in 0.139, 0.251) {
                Add-Tube $vertices $faces 0 $y 0.018 0.087 0.23 40 0 0 0.008
                Add-Tube $vertices $faces 0 $y 0.226 0.093 0.022 40 0 0 0.009
            }
            foreach ($sign in -1, 1) {
                Add-Tube $vertices $faces ($sign * 0.065) 0.152 -0.036 0.10 0.012 40 ($sign * [Math]::PI / 2)
            }
        }
        # StrikeShield Lynx exterior tile: paired recessed vertical apertures, not a drum array.
        "amapads" {
            Add-ChamferBox $vertices $faces 0 0 -0.034 0.31 0.285 0.036 0.014
            foreach ($x in -0.076, 0.076) {
                Add-ChamferBox $vertices $faces $x 0.028 0.003 0.054 0.227 0.018 0.012 1
                Add-Box $vertices $faces $x 0.041 0.021 0.034 0.20 0.003 2
                Add-Box $vertices $faces ($x - 0.026) 0.028 0.023 0.012 0.227 0.018
                Add-Box $vertices $faces ($x + 0.026) 0.028 0.023 0.012 0.227 0.018
                foreach ($y in 0.025, 0.262) { Add-Tube $vertices $faces $x $y 0.004 0.014 0.008 12 0 }
            }
            foreach ($x in -0.138, 0.138) {
                foreach ($y in 0.018, 0.267) { Add-Tube $vertices $faces $x $y 0.002 0.024 0.006 20 0 }
            }
        }
        # Placeholder: no verified Zaslin variant photo; legacy exterior pod layout.
        "zaslin" {
            Add-Drum $vertices $faces 0 0.03 0 0.28 0.06 12
            Add-Drum $vertices $faces -0.125 0.09 0 0.10 0.13 8
            Add-Drum $vertices $faces 0.125 0.09 0 0.10 0.13 8
            Add-Box $vertices $faces 0 0.09 0.07 0.11 0.07 0.11
            Add-Box $vertices $faces 0 0.16 -0.08 0.09 0.05 0.07
        }
    }
    # Accumulate wall normals before splitting UV seams. Caps and housing edges stay hard.
    $smooth = @{}
    foreach ($face in $faces) {
        $a = 3 * $face.v[0]; $b = 3 * $face.v[2]; $c = 3 * $face.v[1]
        $u = @(0..2 | ForEach-Object { $vertices[$b + $_] - $vertices[$a + $_] })
        $v = @(0..2 | ForEach-Object { $vertices[$c + $_] - $vertices[$a + $_] })
        $n = @(($u[1] * $v[2] - $u[2] * $v[1]), ($u[2] * $v[0] - $u[0] * $v[2]), ($u[0] * $v[1] - $u[1] * $v[0]))
        $length = [Math]::Sqrt($n[0] * $n[0] + $n[1] * $n[1] + $n[2] * $n[2])
        if ($length -le 1e-12) { throw "Degenerate source face" }
        $face.normal = @($n | ForEach-Object { $_ / $length })
        if ($face.ContainsKey("smooth")) {
            foreach ($index in $face.v) {
                $key = "$($face.smooth):$index"
                if (-not $smooth.ContainsKey($key)) { $smooth[$key] = @(0.0, 0.0, 0.0) }
                for ($axis = 0; $axis -lt 3; $axis++) { $smooth[$key][$axis] += $face.normal[$axis] }
            }
        }
    }
    $split = [System.Collections.Generic.List[double]]::new()
    $normals = [System.Collections.Generic.List[double]]::new()
    $uvs = [System.Collections.Generic.List[double]]::new()
    $mapped = [System.Collections.Generic.List[object]]::new()
    foreach ($face in $faces) {
        $surface = if ($face.ContainsKey("surface")) { [int]$face.surface } else { 0 }
        $tile = if ($surface -eq 0) { if ($Shape -in @("trophy", "ironfist")) { 1 } else { 0 } } elseif ($surface -eq 1) { 2 } else { 3 }
        $n = $face.normal
        $drop = 0
        for ($axis = 1; $axis -lt 3; $axis++) { if ([Math]::Abs($n[$axis]) -gt [Math]::Abs($n[$drop])) { $drop = $axis } }
        $axes = @(0..2 | Where-Object { $_ -ne $drop })
        $s = @($face.v | ForEach-Object { $vertices[3 * $_ + $axes[0]] })
        $t = @($face.v | ForEach-Object { $vertices[3 * $_ + $axes[1]] })
        $minS = ($s | Measure-Object -Minimum).Minimum; $maxS = ($s | Measure-Object -Maximum).Maximum
        $minT = ($t | Measure-Object -Minimum).Minimum; $maxT = ($t | Measure-Object -Maximum).Maximum
        $indices = @()
        for ($corner = 0; $corner -lt $face.v.Count; $corner++) {
            $index = $face.v[$corner]; $indices += [int]($split.Count / 3)
            for ($axis = 0; $axis -lt 3; $axis++) { $split.Add($vertices[3 * $index + $axis]) }
            $normal = if ($face.ContainsKey("smooth")) { $smooth["$($face.smooth):$index"] } else { $face.normal }
            $length = [Math]::Sqrt($normal[0] * $normal[0] + $normal[1] * $normal[1] + $normal[2] * $normal[2])
            foreach ($value in $normal) { $normals.Add($value / $length) }
            if ($face.ContainsKey("uv")) { $su = $face.uv[2 * $corner]; $tv = $face.uv[2 * $corner + 1] }
            else {
                $su = ($s[$corner] - $minS) / [Math]::Max(1e-9, $maxS - $minS)
                $tv = ($t[$corner] - $minT) / [Math]::Max(1e-9, $maxT - $minT)
            }
            $uvs.Add(($tile % 2) * 0.5 + 0.02 + $su * 0.46)
            $uvs.Add([Math]::Floor($tile / 2) * 0.5 + 0.02 + $tv * 0.46)
        }
        $mapped.Add(@{ v = $indices; t = $face.t; surface = $surface })
    }
    return @{ vertices = @($split); normals = @($normals); uv = @($uvs); faces = @($mapped) }
}

# Depth-buffered triangles use the same reversed fan as APSModelAssets, not painter's
# sorting (which hid recessed faces and drew rear surfaces over front housings).
if (-not ("SAAPSMeshPreview" -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class SAAPSMeshPreview {
    public static void Atlas(string path) {
        using (var bitmap = new Bitmap(512,512,PixelFormat.Format32bppArgb)) {
            var random = new Random(4040);
            Color[] colours = {Color.FromArgb(106,122,77),Color.FromArgb(171,153,109),Color.FromArgb(53,58,49),Color.FromArgb(42,55,57)};
            for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++) {
                int tile = (x/256)+2*(1-y/256), sx = x%256, sy = y%256;
                var colour = colours[tile];
                int noise = random.Next(-5,6);
                int edge = Math.Min(Math.Min(sx,255-sx),Math.Min(sy,255-sy));
                if (edge < 13) noise -= 12;
                if (edge >= 13 && edge <= 15) noise += 14;
                if (tile == 3 && sx%18 == 0) noise -= 8;
                if (tile == 2 && sy%24 == 0) noise -= 5;
                bitmap.SetPixel(x,y,Color.FromArgb(255,Math.Max(0,colour.R+noise),Math.Max(0,colour.G+noise),Math.Max(0,colour.B+noise)));
            }
            bitmap.Save(path,ImageFormat.Png);
        }
    }
    public static void Save(double[] vertices, double[] normals, double[] uv, int[] triangles, string atlasPath, string path, int size, double yaw, double tilt) {
        int count = vertices.Length / 3;
        double[] x = new double[count], y = new double[count], z = new double[count];
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw), ct = Math.Cos(tilt), st = Math.Sin(tilt);
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        for (int i = 0; i < count; i++) {
            double vx = vertices[3*i], vy = vertices[3*i+1], vz = vertices[3*i+2];
            x[i] = vx*cy-vz*sy;
            y[i] = vx*sy*st-vy*ct+vz*cy*st;
            z[i] = vx*sy*ct+vy*st+vz*cy*ct;
            minX = Math.Min(minX,x[i]); maxX = Math.Max(maxX,x[i]);
            minY = Math.Min(minY,y[i]); maxY = Math.Max(maxY,y[i]);
        }
        double extent = Math.Max(maxX-minX,maxY-minY);
        if (extent <= 0) throw new ArgumentException("Empty preview bounds");
        double scale = size*0.82/extent;
        for (int i = 0; i < count; i++) {
            x[i] = size/2.0+(x[i]-(minX+maxX)/2)*scale;
            y[i] = size/2.0+(y[i]-(minY+maxY)/2)*scale;
        }
        int[] texture = new int[512*512];
        using (var atlas = new Bitmap(atlasPath)) {
            for (int py = 0; py < 512; py++) for (int px = 0; px < 512; px++) texture[py*512+px] = atlas.GetPixel(px,py).ToArgb();
        }
        int[] pixels = new int[size*size];
        double[] depth = new double[pixels.Length];
        for (int i = 0; i < depth.Length; i++) depth[i] = double.NegativeInfinity;
        for (int t = 0; t < triangles.Length; t += 3) {
            int a = triangles[t], b = triangles[t+1], c = triangles[t+2];
            double ux = vertices[3*b]-vertices[3*a], uy = vertices[3*b+1]-vertices[3*a+1], uz = vertices[3*b+2]-vertices[3*a+2];
            double vx = vertices[3*c]-vertices[3*a], vy = vertices[3*c+1]-vertices[3*a+1], vz = vertices[3*c+2]-vertices[3*a+2];
            double nx = uy*vz-uz*vy, ny = uz*vx-ux*vz, nz = ux*vy-uy*vx;
            double length = Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if (length < 1e-12 || nx*sy*ct+ny*st+nz*cy*ct <= 0) continue;
            // Lighting interpolates exported normals, including continuous tube UV seams.
            double area = (y[b]-y[c])*(x[a]-x[c])+(x[c]-x[b])*(y[a]-y[c]);
            if (Math.Abs(area) < 1e-10) continue;
            int left = Math.Max(0,(int)Math.Floor(Math.Min(x[a],Math.Min(x[b],x[c]))));
            int right = Math.Min(size-1,(int)Math.Ceiling(Math.Max(x[a],Math.Max(x[b],x[c]))));
            int top = Math.Max(0,(int)Math.Floor(Math.Min(y[a],Math.Min(y[b],y[c]))));
            int bottom = Math.Min(size-1,(int)Math.Ceiling(Math.Max(y[a],Math.Max(y[b],y[c]))));
            for (int py = top; py <= bottom; py++) for (int px = left; px <= right; px++) {
                double p = px+0.5, q = py+0.5;
                double wa = ((y[b]-y[c])*(p-x[c])+(x[c]-x[b])*(q-y[c]))/area;
                double wb = ((y[c]-y[a])*(p-x[c])+(x[a]-x[c])*(q-y[c]))/area;
                double wc = 1-wa-wb;
                if (wa < -1e-9 || wb < -1e-9 || wc < -1e-9) continue;
                int index = py*size+px;
                double d = wa*z[a]+wb*z[b]+wc*z[c];
                if (d <= depth[index]) continue;
                double u = wa*uv[2*a]+wb*uv[2*b]+wc*uv[2*c];
                double v = wa*uv[2*a+1]+wb*uv[2*b+1]+wc*uv[2*c+1];
                var colour = Color.FromArgb(texture[Math.Min(511,Math.Max(0,(int)((1-v)*511)))*512+Math.Min(511,Math.Max(0,(int)(u*511)))]);
                double snx = wa*normals[3*a]+wb*normals[3*b]+wc*normals[3*c];
                double sny = wa*normals[3*a+1]+wb*normals[3*b+1]+wc*normals[3*c+1];
                double snz = wa*normals[3*a+2]+wb*normals[3*b+2]+wc*normals[3*c+2];
                double nl = Math.Sqrt(snx*snx+sny*sny+snz*snz);
                double light = 0.62+0.55*Math.Max(0,(-0.4*snx+0.8*sny+0.4*snz)/Math.Max(1e-12,nl)/Math.Sqrt(0.96));
                depth[index] = d;
                int red = Math.Min(255,(int)(colour.R*light)), green = Math.Min(255,(int)(colour.G*light)), blue = Math.Min(255,(int)(colour.B*light));
                if (size == 256) { red = 150+red*105/255; green = 150+green*105/255; blue = 150+blue*105/255; }
                pixels[index] = Color.FromArgb(255,red,green,blue).ToArgb();
            }
        }
        // Subtle depth-discontinuity lines expose recesses in a single-material preview.
        // This is an illustration, not a claim about Unity's lighting or shadows.
        double edgeThreshold = extent/(size*0.82)*2;
        for (int py = 1; py < size-1; py++) for (int px = 1; px < size-1; px++) {
            int index = py*size+px;
            if (pixels[index] == 0) continue;
            bool edge = false;
            foreach (int offset in new int[] {-1,1,-size,size}) {
                if (double.IsNegativeInfinity(depth[index+offset]) ||
                    depth[index]-depth[index+offset] > edgeThreshold) { edge = true; break; }
            }
            if (edge) {
                Color colour = Color.FromArgb(pixels[index]);
                int floor = size == 256 ? 150 : 0;
                pixels[index] = Color.FromArgb(255,Math.Max(floor,(int)(colour.R*0.72)),Math.Max(floor,(int)(colour.G*0.72)),Math.Max(floor,(int)(colour.B*0.72))).ToArgb();
            }
        }
        using (var bitmap = new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
            var data = bitmap.LockBits(new Rectangle(0,0,size,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels,0,data.Scan0,pixels.Length); }
            finally { bitmap.UnlockBits(data); }
            bitmap.Save(path,ImageFormat.Png);
        }
    }
}
'@
}

function Write-Icon($Mesh, [string]$Path, [int]$Size = 256, [double]$Yaw = 0.65, [double]$Tilt = 0.3) {
    $triangles = [System.Collections.Generic.List[int]]::new()
    foreach ($face in $Mesh.faces) {
        for ($i = 2; $i -lt $face.v.Count; $i++) {
            $triangles.Add([int]$face.v[0]); $triangles.Add([int]$face.v[$i]); $triangles.Add([int]$face.v[$i - 1])
        }
    }
    [SAAPSMeshPreview]::Save([double[]]$Mesh.vertices, [double[]]$Mesh.normals, [double[]]$Mesh.uv, $triangles.ToArray(), $atlasPath, $Path, $Size, $Yaw, $Tilt)
}

[SAAPSMeshPreview]::Atlas($atlasPath)
$guids = @{}
foreach ($system in $systems) {
    if ($guids.ContainsKey($system.Guid)) { throw "Duplicate APS part GUID: $($system.Guid)" }
    $guids[$system.Guid] = $true
    $name = "saaps_$($system.Id)"
    $year = [int]$system.Year
    $mesh = New-SystemMesh $system.Shape
    $mesh | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $meshesDir "$name.json") -Encoding utf8
    Write-Icon $mesh (Join-Path $iconsDir "$name.png")
    Write-Icon $mesh (Join-Path $previewsDir "$name-front.png") 768 0 0
    Write-Icon $mesh (Join-Path $previewsDir "$name-side.png") 768 ([Math]::PI / 2) 0
    Write-Icon $mesh (Join-Path $previewsDir "$name-isometric.png") 768
    $part = [ordered]@{
        v = "0.2"
        guid = $system.Guid
        name = $name
        tags = @("cannon", "scope", "saapsPart", "aps_$($system.Id)")
        date = "1945.09.03"
        order = 0
        transform = [ordered]@{
            mirroring = $true; duplication = $true; pickup = $true; snapToCentre = $true
            autoRotate = $false; airPlacement = $true; translation = $true; rotation = $true
            scaling = $true; dynamic = $true; placementMode = "upFaceNormal"
            scale = @(1.0, 1.0, 1.0); maxScale = @(3.0, 3.0, 3.0); minScale = @(0.25, 0.25, 0.25); objects = @()
        }
        components = @(
            [ordered]@{ fileID = $name; type = "model"; info = [ordered]@{
                v = "0.2"; meshGuid = "691897f033278e64d8640a39162942bb"
                materialGuids = @("5fd2544ba4a8a7a44b737c00a79f74f3")
                scaling = $true; flipping = $true; selectable = $true; placementSurface = $false
                damageable = $false
            } }
        )
    }
    $partPath = Join-Path $partsDir "${name}Part.json"
    $part | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $partPath -Encoding utf8
    $safeName = [System.Security.SecurityElement]::Escape($system.Name)
    $description = [System.Security.SecurityElement]::Escape("$($system.Name) APS module ($year), $($system.Country). Place on the vehicle to enable its simulated protection system.")
    $xml = "<?xml version=`"1.0`" encoding=`"utf-16`"?>`r`n<Part>`r`n  <name>$safeName</name>`r`n  <description>$description</description>`r`n</Part>`r`n"
    [System.IO.File]::WriteAllText((Join-Path $localizationDir "$name.xml"), $xml, [System.Text.Encoding]::Unicode)
}

# Native editable prefab: runtime marks only structures created while loading this exact catalog GUID.
$customMesh = New-BoxMesh 0.125 0.10 0.15
$customPart = [ordered]@{
    v = "0.0"; guid = "a91bb31a-2cf1-4d0a-8e31-000000000011"; name = "saaps_custom"
    tags = @("plateStructurePrefab", "saapsCustomPart"); transform = $null
    components = @([ordered]@{ fileID = "plateStructurePrefab"; type = "plateStructurePrefab"; info = [ordered]@{
        v = "0.0"; mesh = [ordered]@{ v = "0.2"; name = "Custom APS"; smoothAngle = 0; gridSize = 1; format = "freeform"
            mesh = [ordered]@{ majorVersion = 0; minorVersion = 3; vertices = $customMesh.vertices; faces = $customMesh.faces }
        }
    } })
}
$customPart | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $partsDir "saaps_customPart.json") -Encoding utf8
Write-Icon (New-SystemMesh "custom") (Join-Path $iconsDir "saaps_custom.png")
[IO.File]::WriteAllText((Join-Path $localizationDir "saaps_custom.xml"), '<?xml version="1.0" encoding="utf-16"?><Part><name>Custom APS</name><description>Editable APS add-on. Use plate tools to shape it, then Custom APS inspector to name, configure, save and load presets.</description></Part>', [Text.Encoding]::Unicode)
Write-Host "Generated $($systems.Count) APS models and one editable Custom APS: $OutputRoot"
