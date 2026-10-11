# Optional IL shape check; requires local Mono.Cecil, not a packaging dependency or data-flow proof.
param([string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Sprocket')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $GameRoot 'BepInEx\plugins\BepInEx.MelonLoader.Loader\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'src\SAAPS\bin\Release\net6.0\SprocketAdvancedAPS.dll'))
try {
    function AllTypes($types) {
        foreach ($type in $types) { $type; AllTypes $type.NestedTypes }
    }
    $buttons = 0
    foreach ($type in (AllTypes $assembly.MainModule.Types)) {
        if ($type.FullName -notlike '*APSCustomInspector*') { continue }
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            $instructions = @($method.Body.Instructions)
            for ($i = 0; $i -lt $instructions.Count; $i++) {
                $call = $instructions[$i]
                if ($call.OpCode.Code.ToString() -notin @('Call', 'Callvirt') -or $call.Operand.ToString() -notlike '*IGUIElementDrawer::Button*') { continue }
                $buttons++
                $constructed = @($instructions[0..$i] | Where-Object {
                    $_.OpCode.Code.ToString() -eq 'Newobj' -and $_.Operand.ToString() -like '*UITooltip::.ctor(System.String,System.String)'
                })
                if ($constructed.Count -eq 0) { throw 'Custom inspector Button lacks a constructed native value-type tooltip.' }
            }
        }
    }
    if ($buttons -ne 1) { throw "Expected one production button helper; found $buttons." }
    Write-Host 'PASS: compiled Custom inspector button helper constructs native UITooltip (offline; live rendering pending).'
} finally { $assembly.Dispose() }
