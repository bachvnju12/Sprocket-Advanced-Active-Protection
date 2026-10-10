param(
    [string]$GameRoot = "D:\SteamLibrary\steamapps\common\Sprocket",
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$partsRoot = Join-Path $PSScriptRoot "generated\Sprocket_Data\StreamingAssets"
$pluginSource = Join-Path $PSScriptRoot "src\SAAPS\bin\Release\net6.0\SprocketAdvancedAPS.dll"
$pluginDir = Join-Path $GameRoot "BepInEx\plugins"
$installDir = Join-Path $GameRoot "BepInEx\plugins\SprocketAdvancedAPS"
$partsDir = Join-Path $GameRoot "Sprocket_Data\StreamingAssets\Parts"
$locDir = Join-Path $GameRoot "Sprocket_Data\StreamingAssets\Localization\en-UK\Parts"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = Join-Path $GameRoot "BepInEx\SprocketAdvancedAPS-backup-$stamp"
$meshesSource = Join-Path $PSScriptRoot "generated\meshes"
$iconsSource = Join-Path $PSScriptRoot "generated\icons"
$atlasSource = Join-Path $PSScriptRoot "generated\textures\saaps_surfaces.png"

if (-not (Test-Path $GameRoot)) { throw "Game root not found: $GameRoot" }

if ($Remove) {
    foreach ($sourceDir in @((Join-Path $partsRoot "Parts"), (Join-Path $partsRoot "Localization\en-UK\Parts"))) {
        if (-not (Test-Path $sourceDir)) { continue }
        $targetDir = if ($sourceDir -like "*Localization*") { $locDir } else { $partsDir }
        foreach ($file in Get-ChildItem $sourceDir -File) {
            $target = Join-Path $targetDir $file.Name
            if (Test-Path $target) { Remove-Item -LiteralPath $target -Force }
        }
    }
    Remove-Item -LiteralPath (Join-Path $pluginDir "SprocketAdvancedAPS.dll") -Force -ErrorAction SilentlyContinue
    if (Test-Path $installDir) { Remove-Item -LiteralPath $installDir -Recurse -Force }
    Write-Host "Removed SAAPS files. Restore any prior files from their backup folder if needed."
    exit 0
}

if (-not (Test-Path $pluginSource)) { throw "Build DLL missing: $pluginSource. Run build.ps1 -SkipDeploy first." }
if (-not (Test-Path (Join-Path $partsRoot "Parts"))) { & (Join-Path $PSScriptRoot "generate-parts.ps1") }
$partFiles = Get-ChildItem (Join-Path $partsRoot "Parts") -Filter "saaps_*Part.json" -File
$locFiles = Get-ChildItem (Join-Path $partsRoot "Localization\en-UK\Parts") -Filter "saaps_*.xml" -File
if ($partFiles.Count -ne 10 -or $locFiles.Count -ne 10) { throw "Expected 10 APS part JSON and 10 localization files." }
$meshFiles = @(Get-ChildItem $meshesSource -Filter "saaps_*.json" -File)
if ($meshFiles.Count -ne 10) { throw "Expected 10 APS model meshes." }
$iconFiles = @(Get-ChildItem $iconsSource -Filter "saaps_*.png" -File)
if ($iconFiles.Count -ne 10) { throw "Expected 10 APS catalog icons." }
if (-not (Test-Path -LiteralPath $atlasSource)) { throw "Expected SAAPS surface atlas." }
foreach ($target in @((Join-Path $pluginDir "SprocketAdvancedAPS.dll"), (Join-Path $installDir "sprocket-mod.json"), (Join-Path $installDir "meshes"), (Join-Path $installDir "icons"), (Join-Path $installDir "textures"))) {
    if (Test-Path $target) {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Copy-Item -LiteralPath $target -Destination $backupDir -Recurse
    }
}

foreach ($file in @($partFiles) + @($locFiles)) {
    $targetDir = if ($file.Extension -eq ".json") { $partsDir } else { $locDir }
    $target = Join-Path $targetDir $file.Name
    if (Test-Path $target) {
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir $file.Name)
    }
}

New-Item -ItemType Directory -Force -Path $pluginDir, $installDir, $partsDir, $locDir | Out-Null
Copy-Item -LiteralPath $pluginSource -Destination (Join-Path $pluginDir "SprocketAdvancedAPS.dll") -Force
Copy-Item -Path (Join-Path $partsRoot "Parts\saaps_*Part.json") -Destination $partsDir -Force
Copy-Item -Path (Join-Path $partsRoot "Localization\en-UK\Parts\saaps_*.xml") -Destination $locDir -Force
Copy-Item -Path (Join-Path $PSScriptRoot "sprocket-mod.json") -Destination $installDir -Force
New-Item -ItemType Directory -Force -Path (Join-Path $installDir "meshes") | Out-Null
Copy-Item -Path (Join-Path $meshesSource "saaps_*.json") -Destination (Join-Path $installDir "meshes") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $installDir "icons") | Out-Null
Copy-Item -Path (Join-Path $iconsSource "saaps_*.png") -Destination (Join-Path $installDir "icons") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $installDir "textures") | Out-Null
Copy-Item -LiteralPath $atlasSource -Destination (Join-Path $installDir "textures\saaps_surfaces.png") -Force
Get-FileHash $pluginSource,(Join-Path $pluginDir "SprocketAdvancedAPS.dll"),$atlasSource,(Join-Path $installDir "textures\saaps_surfaces.png") -Algorithm SHA256 | Format-Table Path,Hash
if (Test-Path $backupDir) { Write-Host "Collision backups: $backupDir" }
Write-Host "Installed SAAPS plugin, 10 model parts, meshes and catalog icons. Restart game; find APS under Firepower > Active Protection Systems. Replace old APS add-on structures with these model parts."
