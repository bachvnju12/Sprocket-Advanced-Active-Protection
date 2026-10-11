# Sprocket Advanced APS

Adds ten Active Protection System modules to Sprocket: **Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS, Zaslin**, plus an experimental editable **Custom APS** part.

- **Hard-kill** intercepts hostile native projectiles.
- **Soft-kill** (Shtora-1, Afganit) jams guided rounds.

> Stats are gameplay approximations, not real-world specifications.

## Requirements

- Sprocket **0.2.55.5** (Windows x64)
- **Sprocket Mod Loader / BepInEx 6 IL2CPP**
- Sprocket Mod API is optional

## Installation

1. Run the game once with BepInEx 6 IL2CPP to generate interop assemblies, then close it.
2. Build the package with `release.ps1` (see [Build](#build-from-source)). The GitHub `1.0.1` release has no ZIP attached.
3. Extract the ZIP next to `Sprocket.exe`, merging `BepInEx` and `Sprocket_Data`.
4. Open **Firepower > Active Protection Systems** in the Vehicle Designer.

The DLL alone is not enough; keep all bundled assets.

```text
Sprocket/
  BepInEx/plugins/
    SprocketAdvancedAPS.dll
    SprocketAdvancedAPS/   (meshes, icons, textures, audio, sprocket-mod.json)
  Sprocket_Data/StreamingAssets/
    Parts/saaps_*Part.json
    Localization/en-UK/Parts/saaps_*.xml
```

**Upgrading from an older APS add-on?** Remove the old parts and place the new ones.

## Usage

APS works automatically in simulation. Each module has its own countermeasure supply and cooldown; misses also consume countermeasures.

- **Soft-kill aiming:** point the emitter along local **+Z**. Jam cone: **±15° horizontal, ±10° vertical**. Silent, no beam.
- **Hard-kill:** 40 mm explosion effect plays only after a verified interception. Every launch attempt uses a synthetic launch sound.

## Configuration

`BepInEx/config/sprocket.advancedaps.cfg` (created on first launch; close the game before editing)

- Global enable, debug logging
- Detection / reaction multipliers
- Per-module enable, range, reaction, interception multipliers
- Hard-kill speed limits
- `Audio/LaunchVolume` (default `0.8`, `0` = mute)

A default template is in `ConfigExamples/sprocket.advancedaps.cfg`. Don't overwrite a customized config with it. HUD and F7 coverage are not implemented.

## Custom APS (experimental)

1. Place **Firepower > Active Protection Systems > Custom APS**.
2. Shape it with the native plate tools (local **+Z** is forward).
3. Set name, profile, type, ranges, FOV, probabilities, ammo, reload, and launch sound in the **Custom APS** inspector section.

**Presets** (`BepInEx/config/SprocketAdvancedAPS/CustomAPS/<id>.json`)

- **Save preset** / **Confirm overwrite** (backup kept) / **Save as new**
- **Refresh preset list** > **Saved preset** > **Load selected preset**
- Presets can't be loaded onto meshes shared with other parts; edit a vertex first.
- Invalid presets and files over 2 MiB are rejected.

## Known limitations

Built with AI assistance. **Back up your vehicle saves before experimenting.**

- Drozd-2, Arena-M, and Zaslin are placeholder models.
- Custom APS save/load, duplicate, and mirror are not fully verified in game.
- Paint/camo sync, tracer visuals, glass reflections, and spatial audio are untested live.

## Troubleshooting

Check `BepInEx/LogOutput.log` for `[SAAPS]` entries. Set `DebugMode = true` for engagement logs.

## Build from source

Requires .NET 6 SDK and the game/mod assemblies.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1   # build ZIP + SHA256 in dist/
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1   # install local build (game closed)
```

The installer backs up replaced SAAPS files and never touches configs, presets, or other mods.

## License

MIT. See [LICENSE](LICENSE).
Repository: [Sprocket-Advanced-Active-Protection](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection)
