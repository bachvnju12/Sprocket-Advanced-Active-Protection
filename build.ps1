# SAAPS build script for Windows.
# Requires: .NET 6 SDK, Sprocket installation with BepInEx IL2CPP interop assemblies.
param(
    [string]$GameRoot = "",
    [string]$Configuration = "Release",
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

# Locate the game root
if (-not $GameRoot) {
    if ($env:SPROCKET_GAME_ROOT) {
        $GameRoot = $env:SPROCKET_GAME_ROOT
    } elseif (Test-Path "D:\SteamLibrary\steamapps\common\Sprocket") {
        $GameRoot = "D:\SteamLibrary\steamapps\common\Sprocket"
    } else {
        # Default: repo is at <game>\SAAPS
        $GameRoot = Split-Path -Parent $PSScriptRoot
    }
}

if (-not (Test-Path $GameRoot)) {
    Write-Error "Game root not found: $GameRoot"
    exit 1
}

$BepInExCore = Join-Path $GameRoot "BepInEx\core"
$GameInterop = Join-Path $GameRoot "BepInEx\interop"

foreach ($p in @($BepInExCore, $GameInterop)) {
    if (-not (Test-Path $p)) {
        Write-Error "Required path not found: $p"
        Write-Error "Install BepInEx 6 IL2CPP and start the game once to generate interop assemblies."
        exit 1
    }
}

$srcPath = Join-Path $PSScriptRoot "src\SAAPS\SAAPS.csproj"
if (-not (Test-Path $srcPath)) {
    Write-Error "Project file not found: $srcPath"
    exit 1
}

$args = @("build", $srcPath, "--configuration", $Configuration)
if ($SkipDeploy) {
    $args += "-p:SkipModDeploy=true"
}
$args += "-p:SprocketGameRoot=`"$GameRoot`""

Write-Host "Building SAAPS..."
Write-Host "  GameRoot: $GameRoot"
Write-Host "  Config:   $Configuration"
Write-Host "  Deploy:   $(-not $SkipDeploy)"

& dotnet @args
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build succeeded."

if (-not $SkipDeploy) {
    $deployDir = Join-Path $GameRoot "BepInEx\plugins\SprocketAdvancedAPS"
    if (-not (Test-Path $deployDir)) {
        New-Item -ItemType Directory -Force -Path $deployDir | Out-Null
    }

    foreach ($sub in @("assets", "Config")) {
        $srcSub = Join-Path $PSScriptRoot $sub
        if (Test-Path $srcSub) {
            Copy-Item -Path $srcSub -Destination $deployDir -Recurse -Force
        }
    }
    Write-Host "Deployed assets and Config to $deployDir"
}

Write-Host "Done."
