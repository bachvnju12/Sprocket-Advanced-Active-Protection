$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "generated"
. (Join-Path $root "generate-parts.ps1") -OutputRoot $output
# Validate outward winding using the loader's reversed fan, including nonzero rotations.
foreach ($shape in "box", "rotatedBox", "tube", "chamfer") {
    $verts = [System.Collections.Generic.List[double]]::new()
    $faces = [System.Collections.Generic.List[object]]::new()
    switch ($shape) {
        "box" { Add-Box $verts $faces 0.1 0.05 -0.1 0.3 0.2 0.15 }
        "rotatedBox" { Add-RotatedBox $verts $faces 0.1 0.05 -0.1 0.3 0.2 0.15 0.4 0.7 }
        "tube" { Add-Tube $verts $faces 0.1 0.05 -0.1 0.15 0.3 24 0.7 0.4 }
        "chamfer" { Add-ChamferBox $verts $faces 0.1 0.05 -0.1 0.3 0.2 0.15 0.025 }
    }
    $centre = @(0.0, 0.0, 0.0)
    for ($i = 0; $i -lt $verts.Count; $i++) { $centre[$i % 3] += $verts[$i] / ($verts.Count / 3) }
    foreach ($face in $faces) {
        for ($i = 2; $i -lt $face.v.Count; $i++) {
            $a = 3 * $face.v[0]; $b = 3 * $face.v[$i]; $c = 3 * $face.v[$i - 1]
            $u = @(0..2 | ForEach-Object { $verts[$b + $_] - $verts[$a + $_] })
            $v = @(0..2 | ForEach-Object { $verts[$c + $_] - $verts[$a + $_] })
            $n = @(($u[1] * $v[2] - $u[2] * $v[1]), ($u[2] * $v[0] - $u[0] * $v[2]), ($u[0] * $v[1] - $u[1] * $v[0]))
            $dot = 0.0
            for ($axis = 0; $axis -lt 3; $axis++) {
                $dot += $n[$axis] * (($verts[$a + $axis] + $verts[$b + $axis] + $verts[$c + $axis]) / 3 - $centre[$axis])
            }
            if ($dot -le 0) { throw "Inward helper triangle: $shape" }
        }
    }
}
$atlasHash = (Get-FileHash $atlasPath -Algorithm SHA256).Hash
[SAAPSMeshPreview]::Atlas($atlasPath)
if ((Get-FileHash $atlasPath -Algorithm SHA256).Hash -ne $atlasHash) { throw "Nondeterministic surface atlas" }
$atlas = [Drawing.Bitmap]::new($atlasPath)
try {
    if ($atlas.Width -ne 512 -or $atlas.Height -ne 512) { throw "Invalid atlas size" }
    $colours = @(foreach ($x in 128, 384) { foreach ($y in 128, 384) { $atlas.GetPixel($x, $y).ToArgb() } })
    if (@($colours | Select-Object -Unique).Count -ne 4) { throw "Missing atlas surface colours" }
} finally { $atlas.Dispose() }
$seamMesh = New-SystemMesh "drozd1"
$groups = @{}
for ($i = 0; $i -lt $seamMesh.vertices.Count / 3; $i++) {
    $key = (@(0..2 | ForEach-Object { [Math]::Round($seamMesh.vertices[3 * $i + $_], 9) }) -join ',')
    if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
    $groups[$key] += $i
}
$seams = 0; $hardEdges = 0
foreach ($group in $groups.Values) {
    for ($a = 0; $a -lt $group.Count; $a++) { for ($b = $a + 1; $b -lt $group.Count; $b++) {
        $i = $group[$a]; $j = $group[$b]; $dot = 0.0
        foreach ($axis in 0..2) { $dot += $seamMesh.normals[3 * $i + $axis] * $seamMesh.normals[3 * $j + $axis] }
        if ([Math]::Abs($seamMesh.uv[2 * $i] - $seamMesh.uv[2 * $j]) -gt 0.45 -and $dot -gt 0.99999) { $seams++ }
        if ($dot -lt 0.1) { $hardEdges++ }
    } }
}
if ($seams -eq 0 -or $hardEdges -eq 0) { throw "Missing continuous UV seam normals or hard cap edges" }
$uvPreview = Join-Path $output "previews\uv-check.png"
$originalUv = $seamMesh.uv
$seamMesh.uv = @($originalUv | ForEach-Object { 0.75 })
Write-Icon $seamMesh $uvPreview
if ((Get-FileHash $uvPreview).Hash -eq (Get-FileHash (Join-Path $output "icons\saaps_drozd1.png")).Hash) { throw "Preview ignores UV data" }
$seamMesh.uv = $originalUv
$parts = Join-Path $output "Sprocket_Data\StreamingAssets\Parts"
$localization = Join-Path $output "Sprocket_Data\StreamingAssets\Localization\en-UK\Parts"
$files = @(Get-ChildItem $parts -Filter "saaps_*Part.json" -File)
$locFiles = @(Get-ChildItem $localization -Filter "saaps_*.xml" -File)
if ($files.Count -ne 11 -or $locFiles.Count -ne 11) { throw "Expected 10 fixed parts plus Custom APS and localization files." }
$guids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$meshes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in $files) {
    $part = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
    if ($part.name -notmatch '^saaps_[a-z0-9]+$') { throw "Invalid part id in $($file.Name)." }
    if (-not $guids.Add($part.guid)) { throw "Duplicate part GUID: $($part.guid)" }
    if (-not (Test-Path (Join-Path $localization "$($part.name).xml"))) { throw "Missing localization: $($part.name)" }
    $image = [Drawing.Bitmap]::new((Join-Path $output "icons\$($part.name).png"))
    try {
        if ($image.Width -ne 256 -or $image.Height -ne 256) { throw "Invalid icon size" }
        $visible = 0; $transparent = 0; $colored = 0
        for ($x = 0; $x -lt 256; $x++) { for ($y = 0; $y -lt 256; $y++) {
            $pixel = $image.GetPixel($x, $y)
            if ($pixel.A -gt 0 -and [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B)) -lt 150) { throw "Icon shading below 150: $($part.name)" }
            if ($x % 4 -eq 0 -and $y % 4 -eq 0) {
                if ($pixel.A -eq 0) { $transparent++ } else { $visible++; if ($pixel.R -lt 240 -or $pixel.G -lt 240 -or $pixel.B -lt 240) { $colored++ } }
            }
        } }
        if ($visible -lt 100 -or $transparent -lt 100 -or $colored -lt 100) { throw "Blank/white APS icon: $($part.name)" }
    } finally { $image.Dispose() }
    if ($part.name -eq 'saaps_custom') {
        if ($part.guid -ne 'a91bb31a-2cf1-4d0a-8e31-000000000011' -or $part.tags -notcontains 'plateStructurePrefab' -or @($part.components).Count -ne 1 -or $part.components[0].type -ne 'plateStructurePrefab') { throw 'Invalid editable Custom APS prefab' }
        $native = $part.components[0].info.mesh
        if ($native.format -ne 'freeform' -or $native.mesh.vertices.Count -ne 24 -or $native.mesh.faces.Count -ne 6) { throw 'Invalid Custom APS starter geometry' }
        continue
    }
    if ($part.tags -notcontains "cannon" -or $part.tags -notcontains "scope" -or $part.tags -notcontains "saapsPart" -or $part.tags -contains "sightsElectronics") { throw "APS menu tags missing or overlap Thermal group: $($file.Name)" }
    if ($part.date -ne '1945.09.03') { throw "APS palette date missing: $($file.Name)" }
    $model = @($part.components | Where-Object { $_.type -eq "model" })
    if ($model.Count -ne 1 -or @($part.components).Count -ne 1 -or $part.transform -eq $null -or $part.tags -contains "plateStructurePrefab") { throw "APS must use a persistent model component: $($file.Name)" }
    if ($model[0].fileID -ne $part.name) { throw "APS model fileID mismatch: $($file.Name)" }
    if (@($model[0].info.materialGuids).Count -ne 1 -or $model[0].info.materialGuids[0] -ne '5fd2544ba4a8a7a44b737c00a79f74f3') { throw "Native exterior material missing: $($file.Name)" }
    if ($part.guid -notmatch '^a91bb31a-2cf1-4d0a-8e31-0000000000(0[1-9]|10)$') { throw "APS part GUID missing or invalid: $($file.Name)" }
    if ($model[0].info.v -ne '0.2' -or -not $model[0].info.selectable -or -not $part.transform.dynamic) { throw "Invalid APS model payload: $($file.Name)" }
    $mesh = Get-Content -Raw -LiteralPath (Join-Path $output "meshes\$($part.name).json") | ConvertFrom-Json
    if (@($mesh.vertices).Count -lt 24 -or @($mesh.vertices).Count % 3 -ne 0 -or @($mesh.faces).Count -lt 6) { throw "Missing mesh geometry: $($file.Name)" }
    foreach ($value in $mesh.vertices) {
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or [Math]::Abs($value) -gt 2) { throw "Invalid mesh coordinate: $($file.Name)" }
    }
    if ($mesh.uv.Count -ne ($mesh.vertices.Count / 3 * 2) -or $mesh.normals.Count -ne $mesh.vertices.Count) { throw "UV/normal count mismatch: $($file.Name)" }
    foreach ($value in $mesh.uv) {
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -lt 0 -or $value -gt 1) { throw "Invalid texture UV: $($file.Name)" }
    }
    for ($i = 0; $i -lt $mesh.normals.Count; $i += 3) {
        $length = 0.0
        foreach ($axis in 0..2) {
            $value = $mesh.normals[$i + $axis]
            if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) { throw "Invalid normal" }
            $length += $value * $value
        }
        if ([Math]::Abs($length - 1) -gt 0.001) { throw "Normal not unit length: $($file.Name)" }
    }
    $triangleCount = 0
    foreach ($face in $mesh.faces) {
        if ($face.surface -notin @(0, 1, 2)) { throw "Invalid material slot: $($file.Name)" }
        if (@($face.v).Count -lt 3 -or @($face.t).Count -ne @($face.v).Count -or @($face.v | Where-Object { $_ -ne [Math]::Truncate($_) -or $_ -lt 0 -or $_ -ge (@($mesh.vertices).Count / 3) }).Count -ne 0) { throw "Invalid mesh face: $($file.Name)" }
        for ($i = 2; $i -lt $face.v.Count; $i++) {
            $a = 3 * $face.v[0]; $b = 3 * $face.v[$i]; $c = 3 * $face.v[$i - 1]
            $u = @(0..2 | ForEach-Object { $mesh.vertices[$b + $_] - $mesh.vertices[$a + $_] })
            $v = @(0..2 | ForEach-Object { $mesh.vertices[$c + $_] - $mesh.vertices[$a + $_] })
            $nx = $u[1] * $v[2] - $u[2] * $v[1]
            $ny = $u[2] * $v[0] - $u[0] * $v[2]
            $nz = $u[0] * $v[1] - $u[1] * $v[0]
            if ($nx * $nx + $ny * $ny + $nz * $nz -lt 1e-20) { throw "Degenerate triangle: $($file.Name)" }
            $triangleCount++
        }
    }
    if ($triangleCount -gt 8000) { throw "APS triangle budget exceeded: $($file.Name) $triangleCount" }
    $meshHash = $mesh | ConvertTo-Json -Depth 20 -Compress
    if (-not $meshes.Add($meshHash)) { throw "APS parts must use visibly distinct geometry: $($file.Name)" }
    foreach ($view in 'front', 'side', 'isometric') {
        $preview = [Drawing.Bitmap]::new((Join-Path $output "previews\$($part.name)-$view.png"))
        try { if ($preview.Width -ne 768 -or $preview.Height -ne 768) { throw "Invalid preview size: $($part.name)-$view" } }
        finally { $preview.Dispose() }
    }
}
Write-Host "PASS: 10 persistent APS model parts, one editable Custom APS prefab, 11 localization/icons and 30 previews (offline schema check, not live game verification)."
