$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\Core\APSCore.cs'))
$definitions = [IO.File]::ReadAllText((Join-Path $root 'src\SAAPS\Systems\APSDefinitions.cs'))
$actual = @{}
$section = ''
foreach ($line in [IO.File]::ReadAllLines((Join-Path $root 'Config\sprocket.advancedaps.cfg'))) {
    if ($line -match '^\[([^\]]+)\]$') { $section = $Matches[1] }
    elseif ($line -match '^([A-Za-z]+)\s*=\s*(\S+)\s*$') {
        $key = "$section/$($Matches[1])"
        if ($actual.ContainsKey($key)) { throw "Duplicate config key: $key" }
        $actual[$key] = $Matches[2]
    } elseif ($line.Trim() -and -not $line.Trim().StartsWith('#')) { throw "Invalid config line: $line" }
}
$expected = @{}
foreach ($match in [regex]::Matches($source, 'config.Bind\("([^"]+)", "([^"]+)", (true|false|[0-9.]+f|Key\.[A-Za-z0-9]+),')) {
    $expected["$($match.Groups[1].Value)/$($match.Groups[2].Value)"] = $match.Groups[3].Value -replace '^Key\.', '' -replace 'f$', ''
}
foreach ($match in [regex]::Matches($definitions, 'Id = "([a-z0-9]+)"(?<body>.*?)(?=Id = "|\z)', [Text.RegularExpressions.RegexOptions]::Singleline)) {
    $id = $match.Groups[1].Value
    $prefix = "Modules.$id"
    $expected["$prefix/Enabled"] = 'true'
    foreach ($key in 'RangeMultiplier', 'ReactionMultiplier', 'InterceptMultiplier') { $expected["$prefix/$key"] = '1' }
    if ($id -eq 'shtora1') { continue }
    $body = $match.Groups['body'].Value
    $minimum = [regex]::Match($body, 'MinEngagementSpeed = ([0-9.]+)f').Groups[1].Value
    $maximum = [regex]::Match($body, 'MaxEngagementSpeed = ([0-9.]+)f').Groups[1].Value
    if (-not $minimum -or -not $maximum) { throw "Speed defaults missing: $id" }
    $expected["$prefix/SpeedBasedInterception"] = 'true'
    $expected["$prefix/MinInterceptSpeed"] = [Math]::Min(50, [double]::Parse($minimum, [Globalization.CultureInfo]::InvariantCulture)).ToString([Globalization.CultureInfo]::InvariantCulture)
    $expected["$prefix/MaxInterceptSpeed"] = $maximum
}
if ($expected.Count -ne 76 -or $actual.Count -ne $expected.Count) { throw "Config key count mismatch: expected=$($expected.Count) actual=$($actual.Count)" }
foreach ($key in $expected.Keys) {
    if (-not $actual.ContainsKey($key)) { throw "Missing config key: $key" }
    $wanted = $expected[$key]
    $value = $actual[$key]
    if ($wanted -match '^[0-9.]+$') {
        if ([double]::Parse($value, [Globalization.CultureInfo]::InvariantCulture) -ne [double]::Parse($wanted, [Globalization.CultureInfo]::InvariantCulture)) { throw "Default mismatch: $key = $value; expected $wanted" }
    } elseif ($value -cne $wanted) { throw "Default mismatch: $key = $value; expected $wanted" }
}
Write-Host 'PASS: 76 config template keys match production bindings and speed defaults.'
