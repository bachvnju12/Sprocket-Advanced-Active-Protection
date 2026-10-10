$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\Core\APSCore.cs'))
$definitions = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\Systems\APSDefinitions.cs'))
$config = @{}; $section = ''
foreach ($line in [IO.File]::ReadAllLines((Join-Path $root 'Config\sprocket.advancedaps.cfg'))) {
    if ($line -match '^\[([^\]]+)\]$') { $section = $Matches[1]; continue }
    if ($line -match '^([A-Za-z]+)\s*=\s*(\S+)\s*$') {
        $key = "$section.$($Matches[1])"
        if ($config.ContainsKey($key)) { throw "Duplicate config key: $key" }
        $config[$key] = $Matches[2]
    } elseif ($line.Trim() -and -not $line.Trim().StartsWith('#')) { throw "Invalid config line: $line" }
}
$expected = @{}
foreach ($binding in [regex]::Matches($source, 'config.Bind\("([^"]+)", "([^"]+)", (true|false|[0-9.]+f|Key\.[A-Za-z0-9]+),')) {
    $expected["$($binding.Groups[1].Value).$($binding.Groups[2].Value)"] = $binding.Groups[3].Value.Replace('Key.', '').TrimEnd('f')
}
foreach ($block in [regex]::Matches($definitions, 'Id = "([a-z0-9]+)"(?<body>.*?)(?=Id = "|\z)', [Text.RegularExpressions.RegexOptions]::Singleline)) {
    $id = $block.Groups[1].Value
    foreach ($key in 'Enabled', 'RangeMultiplier', 'ReactionMultiplier', 'InterceptMultiplier') {
        $expected["Modules.$id.$key"] = $(if ($key -eq 'Enabled') { 'true' } else { '1' })
    }
    if ($id -eq 'shtora1') { continue }
    $body = $block.Groups['body'].Value
    $min = [double][regex]::Match($body, 'MinEngagementSpeed = ([0-9.]+)f').Groups[1].Value
    $max = [regex]::Match($body, 'MaxEngagementSpeed = ([0-9.]+)f').Groups[1].Value
    $expected["Modules.$id.SpeedBasedInterception"] = 'true'
    $expected["Modules.$id.MinInterceptSpeed"] = [Math]::Min(50, $min).ToString([Globalization.CultureInfo]::InvariantCulture)
    $expected["Modules.$id.MaxInterceptSpeed"] = $max
}
if ($expected.Count -ne 75 -or $config.Count -ne $expected.Count) { throw 'Expected 75 runtime config keys' }
foreach ($key in $expected.Keys) {
    $value = $expected[$key]
    $match = $config.ContainsKey($key) -and $config[$key] -eq $value
    if ($value -match '^[0-9.]+$' -and $config.ContainsKey($key)) {
        $match = [double]::Parse($config[$key], [Globalization.CultureInfo]::InvariantCulture) -eq [double]::Parse($value, [Globalization.CultureInfo]::InvariantCulture)
    }
    if (-not $match) { throw "Config default mismatch: $key (expected $value)" }
}
Write-Host 'PASS: 75 config template keys match production bindings and speed defaults.'
