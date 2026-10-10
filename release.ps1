# Build and package only SAAPS. Never deploy or copy a user's game config.
param([string]$GameRoot = "", [string]$OutputRoot = "")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $root "dist" }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)

& (Join-Path $root "tests\check-config.ps1")
& (Join-Path $root "tests\check-parts.ps1")
& dotnet run --project (Join-Path $root "tests\RegistryCheck.csproj") -- (Join-Path $root "generated\Sprocket_Data\StreamingAssets\Parts")
if ($LASTEXITCODE -ne 0) { throw "Registry regression failed: $LASTEXITCODE" }
& (Join-Path $root "build.ps1") -GameRoot $GameRoot -Configuration Release -SkipDeploy

$main = [IO.File]::ReadAllText((Join-Path $root "src\SAAPS\Main.cs"))
$version = [regex]::Match($main, 'ModVersion = "([0-9]+\.[0-9]+\.[0-9]+)"').Groups[1].Value
if (-not $version) { throw "ModVersion missing" }
$name = "SprocketAdvancedAPS-$version-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$stage = Join-Path $OutputRoot $name
$zip = "$stage.zip"
if ((Test-Path $stage) -or (Test-Path $zip)) { throw "Release output already exists: $stage" }
$plugin = Join-Path $stage "BepInEx\plugins"
$assets = Join-Path $plugin "SprocketAdvancedAPS"
$parts = Join-Path $stage "Sprocket_Data\StreamingAssets\Parts"
$loc = Join-Path $stage "Sprocket_Data\StreamingAssets\Localization\en-UK\Parts"
$examples = Join-Path $stage "ConfigExamples"
New-Item -ItemType Directory -Path $plugin, $assets, $parts, $loc, $examples -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root "src\SAAPS\bin\Release\net6.0\SprocketAdvancedAPS.dll") -Destination $plugin
Copy-Item -LiteralPath (Join-Path $root "sprocket-mod.json") -Destination $assets
foreach ($dir in "meshes", "icons", "textures") {
    Copy-Item -LiteralPath (Join-Path $root "generated\$dir") -Destination $assets -Recurse
}
Copy-Item -Path (Join-Path $root "generated\Sprocket_Data\StreamingAssets\Parts\saaps_*Part.json") -Destination $parts
Copy-Item -Path (Join-Path $root "generated\Sprocket_Data\StreamingAssets\Localization\en-UK\Parts\saaps_*.xml") -Destination $loc
Copy-Item -LiteralPath (Join-Path $root "Config\sprocket.advancedaps.cfg") -Destination $examples
foreach ($file in "README.md", "INSTALL.txt", "CHANGELOG.md", "CREDITS.md", "VERIFICATION.md", "LICENSE") {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}

# Hash every packaged file, then compare the ZIP's uncompressed bytes to the staging files.
$files = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName)
$manifest = foreach ($file in $files) {
    $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
    "$( (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() )  $relative"
}
[IO.File]::WriteAllLines((Join-Path $stage "SHA256SUMS.txt"), [string[]]$manifest, [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
        $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $expected = @(Get-ChildItem -LiteralPath $stage -File -Recurse)
    if ($archive.Entries.Count -ne $expected.Count) { throw "ZIP entry count mismatch" }
    foreach ($entry in $archive.Entries) {
        $stream = $entry.Open()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose() }
        $local = Join-Path $stage $entry.FullName
        if ($hash -ne (Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash) { throw "ZIP hash mismatch: $($entry.FullName)" }
    }
} finally { $sha.Dispose(); $archive.Dispose() }
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$zipHash  $name.zip`n", [Text.UTF8Encoding]::new($false))
Write-Host "PASS: $($expected.Count) ZIP files verified. Release: $zip"
Write-Host "SHA256: $zipHash"
