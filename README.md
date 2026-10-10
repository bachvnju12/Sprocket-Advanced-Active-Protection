# Sprocket Advanced APS — Installation

Adds ten placeable Active Protection System modules to Sprocket: Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS and Zaslin.

Hard-kill modules intercept hostile native projectiles. Shtora-1 and Afganit also provide simulated soft-kill against guided rounds. Effectiveness, speeds and coverage are gameplay approximations, not confirmed real-world specifications.

## Requirements

- Sprocket **0.2.55.5**, Windows x64.
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup.
- **Sprocket Mod API >= 1.1.0, < 2.0.0**.
- Quality of Life and other optional mods are not required.

## Install

1. Start the game once with the mod loader and Sprocket Mod API installed, then close it.
2. Download the `SprocketAdvancedAPS-1.0.0-*.zip` release.
3. Extract the ZIP into the Sprocket installation folder, next to `Sprocket.exe`. Merge the included `BepInEx` and `Sprocket_Data` folders.
4. Launch the game. Open **Firepower > Active Protection Systems** in the Vehicle Designer and place an APS module on your vehicle.

**The DLL alone is not enough.** Keep the meshes, icons, texture atlas, part definitions and localization included in the ZIP.

Expected layout:

```text
Sprocket/
  BepInEx/plugins/
    SprocketAdvancedAPS.dll
    SprocketAdvancedAPS/
      sprocket-mod.json
      meshes/
      icons/
      textures/
  Sprocket_Data/StreamingAssets/
    Parts/saaps_*Part.json
    Localization/en-UK/Parts/saaps_*.xml
```

Upgrading from older APS add-on structures? Remove and replace those parts in the designer. Updating the DLL does not convert old structures into the new persistent APS model parts.

## Use

APS operates automatically during simulation. Each placed module has its own countermeasure supply and cooldown; misses also consume countermeasures.

Point soft-kill emitters along their local **+Z** axis toward the desired arc. Each emitter's jam cone is **30° horizontal total (±15°), 20° vertical total (±10°)**. Afganit's wider hard-kill coverage is separate. Soft-kill does not draw a beam or fire an interceptor.

Hard-kill uses a native shell visual adapter and requests a **40 mm explosion effect only after a verified interception**. Visual failure does not prevent interception. Native tracer appearance and vehicle paint/camo synchronization still need live testing.

## Configuration

BepInEx creates the runtime config on first load:

```text
Sprocket/BepInEx/config/sprocket.advancedaps.cfg
```

Close the game before editing. Settings include global enable, debug logs, detection/reaction multipliers, per-module enable/range/reaction/interception multipliers, and hard-kill speed limits.

The ZIP includes a clean default template at:

```text
ConfigExamples/sprocket.advancedaps.cfg
```

Use it only if needed; **do not overwrite a customized config**. Release extraction does not install the template into `BepInEx/config`.

HUD, F7 coverage and designer inspector rendering are not implemented. `ShowHUD` and `ToggleHUD` are reserved settings. `Config/default-aps-profiles.json` in the source repository is reference data, not a runtime custom-profile loader.

## Status and troubleshooting

Offline tests check configuration defaults, generated part identity, mesh geometry and gameplay helper rules. They do not prove live game behavior. Models are simplified: three reference-based drafts and seven approximate layouts, not photo-matched replicas. Paint/camo, tracer appearance and save/load behavior need live validation; see [VERIFICATION.md](VERIFICATION.md).

Yep, this was built with AI assistance. **Back up your vehicle saves before experimenting.**

Having trouble? Check `BepInEx/LogOutput.log` for `[SAAPS]` messages. Enable `DebugMode = true` in the config for engagement decisions. HUD is not needed for APS to operate.

## Build from source

With .NET 6 SDK and the required game/mod assemblies installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1
```

This runs offline tests, builds Release without deploying to the game, and creates a ZIP plus SHA256 checksum under `dist/`. Share the ZIP and `.sha256` file as release assets; do not commit local `bin/`, `obj/`, `work/` or `dist/` output.

## License and credits

GPL-3.0-only. See [LICENSE](LICENSE) and [CREDITS.md](CREDITS.md).
