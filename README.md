# Sprocket Advanced APS — Installation

Adds ten fixed Active Protection System modules to Sprocket: Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS and Zaslin. Current development source also includes an editable **Custom APS** part and shape/settings presets; native placement and save/load acceptance are still pending.

Hard-kill modules intercept hostile native projectiles. Shtora-1 and Afganit also provide simulated soft-kill against guided rounds. Effectiveness, speeds and coverage are gameplay approximations, not confirmed real-world specifications.

## Requirements

- Sprocket **0.2.55.5**, Windows x64.
- A working **Sprocket Mod Loader / BepInEx 6 IL2CPP** setup.
- Sprocket Mod API is optional. SAAPS owns its APS menu and icons; neither requires the MelonLoader 0.3.0.0 API nor the BepInEx 1.1.x API. Native/live checks for both setups remain pending.
- Quality of Life and other optional mods are not required.

## Install

1. Start the game once with BepInEx 6 IL2CPP installed to generate interop assemblies, then close it.
2. Download the `SprocketAdvancedAPS-1.0.2-*.zip` release.
3. Extract the ZIP into the Sprocket installation folder, next to `Sprocket.exe`. Merge the included `BepInEx` and `Sprocket_Data` folders.
4. Launch the game. Open **Firepower > Active Protection Systems** in the Vehicle Designer and place an APS module on your vehicle.

**The DLL alone is not enough.** Keep the meshes, icons, texture atlas, audio, part definitions and localization included in the ZIP. Older published ZIPs do not contain the current development changes. Registry installation remains blocked until its multi-directory asset mapping is supported and tested.

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
      audio/{shot,rocket,launcher}.wav
  Sprocket_Data/StreamingAssets/
    Parts/saaps_*Part.json
    Localization/en-UK/Parts/saaps_*.xml
```

Upgrading from older APS add-on structures? Remove and replace those parts in the designer. Updating the DLL does not convert old structures into the new persistent APS model parts.

## Use

APS operates automatically during simulation. Each placed module has its own countermeasure supply and cooldown; misses also consume countermeasures.

Point soft-kill emitters along their local **+Z** axis toward the desired arc. Each emitter's jam cone is **30° horizontal total (±15°), 20° vertical total (±10°)**. Afganit's wider hard-kill coverage is separate. Soft-kill does not draw a beam or fire an interceptor.

Hard-kill uses a native shell visual adapter and requests a **40 mm explosion effect only after a verified interception**. Visual failure does not prevent interception. Native tracer appearance and vehicle paint/camo synchronization still need live testing.

Shtora-1 uses an uncovered IR-filter variant: recessed wine-red opaque glossy glass inside a painted bezel. Glass keeps its own color; housing retains native vehicle paint/camo. Tint/depth are modeling estimates; the local reference photo has its painted cover fitted. No emission or beam is added. HDRP reflections and camo separation remain unverified in game.

## Custom APS — development feature

1. Open **Firepower > Active Protection Systems > Custom APS**. Selection uses the native editable plate loader. Only a successfully marked Custom plate gets the APS inspector; ordinary plates remain unchanged. This revision still needs in-game acceptance.
2. Use native plate vertex/edge/face tools for its shape. Local **+Z** is forward. Open **Custom APS** in the plate inspector to set a Unicode name, starting profile, type, ranges, full-angle FOV, probabilities, ammo/reload and launch sound.
3. **Save preset** stores shape and settings. Existing files require **Confirm overwrite (backup kept)**; **Save as new** creates a separate ID. Use **Refresh preset list**, **Saved preset**, then **Load selected preset** on another Custom APS part.
4. Presets live at `BepInEx/config/SprocketAdvancedAPS/CustomAPS/<id>.json`. Vehicle serialization is intended to keep its own settings snapshot and native geometry, independent of the external preset. Quit/restart/load, duplicate and mirror still require live verification.

Shared native meshes are rejected during preset load to avoid changing other parts. Use native vertex editing to obtain a separate mesh before retrying; this workflow remains unverified live. Presets reject invalid fields, geometry and files larger than 2 MiB. If both apply and rollback fail, do not save over your original vehicle.

Hard-kill launch attempts, including misses, use three original **synthetic** impulse/noise sound profiles. No real APS firing recording was listened to or matched. Soft-kill is silent. `Audio/LaunchVolume` defaults to `0.8`; `0` mutes launch SFX.

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

HUD and F7 coverage are not implemented. `ShowHUD` and `ToggleHUD` are reserved settings. Custom APS has a native plate inspector extension; fixed-model balance remains in the config. `Config/default-aps-profiles.json` is reference data, not the Custom APS preset format.

## Status and troubleshooting

Offline tests check metadata, configuration, generated parts, mesh geometry, preset validation/atomic writes, WAV decoding and production module rules. They do not prove live game behavior. Seven models are photo-reference drafts; Drozd-2, Arena-M and Zaslin remain placeholders. None are measured or photo-matched replicas. Custom placement/persistence, paint/camo, tracer appearance and spatial audio need live validation; see [VERIFICATION.md](VERIFICATION.md).

Yep, this was built with AI assistance. **Back up your vehicle saves before experimenting.**

Having trouble? Check `BepInEx/LogOutput.log` for `[SAAPS]` messages. Enable `DebugMode = true` in the config for engagement decisions. HUD is not needed for APS to operate.

## Build from source

With .NET 6 SDK and the required game/mod assemblies installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1
```

This runs offline tests, builds Release without deploying to the game, and creates a ZIP plus SHA256 checksum under `dist/`. Packaging does not complete live acceptance or the licensing audit. When publishing is separately approved, use `RELEASE_NOTES.md` as the release body so its `sp-compat` comment is present in the body, not only an attachment. Do not commit local `bin/`, `obj/`, `work/` or `dist/` output.

## License and credits

MIT, matching [LICENSE](LICENSE). Maintainer/repository author: **bachvnju12**, [Sprocket-Advanced-Active-Protection](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection). Existing copyright notice names `trongbach672011-ux`; it is retained, not treated as proof that these accounts belong to the same person. Third-party licenses remain separate; see [CREDITS.md](CREDITS.md).
