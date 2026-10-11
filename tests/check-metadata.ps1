$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$author = 'bachvnju12'
$repo = 'bachvnju12/Sprocket-Advanced-Active-Protection'
$manifest = [IO.File]::ReadAllText((Join-Path $root 'sprocket-mod.json')) | ConvertFrom-Json
[xml]$project = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\SAAPS.csproj'))
$main = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\Main.cs'))
$license = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
if (@($manifest.authors).Count -ne 1 -or $manifest.authors[0] -cne $author -or $manifest.repository -cne $repo -or $manifest.license -cne 'MIT') { throw 'Manifest author/repository/license mismatch' }
if ($project.Project.PropertyGroup.Authors -cne $author -or $project.Project.PropertyGroup.RepositoryUrl -cne "https://github.com/$repo" -or $project.Project.PropertyGroup.PackageLicenseExpression -cne 'MIT') { throw 'Project author/repository/license mismatch' }
foreach ($pair in @(@('Authors', $author), @('Repository', $repo), @('License', 'MIT'))) {
    if (-not $main.Contains("AssemblyMetadata(`"Sprocket.Mod.$($pair[0])`", `"$($pair[1])`")")) { throw "Assembly metadata mismatch: $($pair[0])" }
}
if (-not $license.StartsWith('MIT License') -or -not $license.Contains('Copyright (c) 2026 trongbach672011-ux')) { throw 'Existing MIT license/copyright notice changed' }
foreach ($dependency in $manifest.dependencies) {
    if (-not $dependency.id -or -not $dependency.version -or -not $dependency.when -or $dependency.PSObject.Properties['required']) { throw 'Dependency must declare id/version/when, not required' }
}
if (-not $main.Contains('[BepInDependency("furryaxw.sprocket-mod-api", BepInDependency.DependencyFlags.SoftDependency)]') -or @($manifest.dependencies | Where-Object { $_.id -eq 'furryaxw.sprocket-mod-api' }).Count -ne 0) { throw 'ModAPI must remain optional for both loader setups' }
if (@($project.Project.ItemGroup.Reference | Where-Object { $_.Include -eq 'SprocketModAPI' }).Count -ne 0) { throw 'SAAPS must not link against either ModAPI version' }
$notes = [IO.File]::ReadAllText((Join-Path $root 'RELEASE_NOTES.md'))
$blocks = [regex]::Matches($notes, '<!--\s*sp-compat\s+(\{[^\r\n]*\})\s*-->')
if ($blocks.Count -ne 1) { throw 'Expected one release compatibility declaration' }
$compat = $blocks[0].Groups[1].Value | ConvertFrom-Json
if ($compat.'hamish.sprocket' -cne '0.2.55.5' -or $compat.'bepinex.bepinex' -cne '6.0.0-be.788') { throw 'Release compatibility values differ from requested declaration' }
$version = [regex]::Match($main, 'ModVersion = "([0-9.]+)"').Groups[1].Value
if ($manifest.version -cne $version -or $project.Project.PropertyGroup.Version -cne $version -or $project.Project.PropertyGroup.AssemblyVersion -cne "$version.0" -or $project.Project.PropertyGroup.FileVersion -cne "$version.0") { throw 'Version mismatch' }
if ($manifest.installation -cne 'manual-full-zip' -or $manifest.PSObject.Properties['install'] -or $manifest.PSObject.Properties['release'] -or $manifest.PSObject.Properties['schema_version']) { throw 'Local metadata must not advertise unsupported registry installation' }
Write-Host 'PASS: local package/project/assembly metadata agree, MIT notice preserved and pinned release-body sp-compat parses (offline; registry CI and licensing audit not exercised).'
