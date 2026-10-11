# Sprocket Advanced APS

Adds 10 Active Protection Systems to Sprocket: **Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS, Zaslin**, plus an experimental **Custom APS**.

- **Hard-kill:** intercepts hostile native projectiles.
- **Soft-kill:** Shtora-1 and Afganit jam guided projectiles.
- **Custom APS:** editable plate, configurable protection and presets.

[**Download v1.0.1**](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection/releases/download/1.0.1/SprocketAdvancedAPS-1.0.1-20261011-131659.zip) · [All releases](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection/releases) · [Report an issue](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection/issues)

## Requirements

- **Sprocket 0.2.55.5** (Windows x64)
- **Sprocket Mod Loader / BepInEx 6 IL2CPP**
- **Sprocket Mod API >= 1.1.0, < 2.0.0**

## Installation

1. Install the required loader and Mod API. Run Sprocket once to generate BepInEx interop assemblies, then close it.
2. Download the [v1.0.1 ZIP](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection/releases/download/1.0.1/SprocketAdvancedAPS-1.0.1-20261011-131659.zip) (**not** GitHub's Source code ZIP).
3. Extract it to the folder containing `Sprocket.exe`, merging `BepInEx` and `Sprocket_Data`.
4. Start the game and find **Firepower > Active Protection Systems** in the Vehicle Designer.

Keep all bundled files; the DLL alone is not enough.

```text
Sprocket/
├── BepInEx/plugins/
│   ├── SprocketAdvancedAPS.dll
│   └── SprocketAdvancedAPS/       (meshes, icons, textures, audio, sprocket-mod.json)
└── Sprocket_Data/StreamingAssets/
    ├── Parts/saaps_*Part.json
    └── Localization/en-UK/Parts/saaps_*.xml
```

**Updating:** close the game, extract the new ZIP, and keep your existing configs and presets. Vehicles using parts from older APS add-ons may need those parts replaced.

## Usage

APS operates automatically in simulation. Each module has its own countermeasure supply and cooldown; failed attempts also use countermeasures.

| Protection | Systems |
| --- | --- |
| Hard-kill | Drozd-1, Drozd-2, Arena, Arena-M, Trophy, Iron Fist, AMAP-ADS, Zaslin |
| Hard-kill + soft-kill | Afganit |
| Soft-kill | Shtora-1 |

- **Soft-kill:** point the emitter along local **+Z**. Jam cone: **±15° horizontal, ±10° vertical**. No visible beam or sound.
- **Hard-kill:** a 40 mm explosion effect plays after confirmed interception; launch attempts use a synthetic launch sound.

## Custom APS (experimental)

1. Place **Firepower > Active Protection Systems > Custom APS**.
2. Shape it using the native plate tools (**+Z** is forward).
3. Set name, profile, type, range, FOV, interception probabilities, ammo, reload and launch sound in the **Custom APS** inspector.

**Presets:** `BepInEx/config/SprocketAdvancedAPS/CustomAPS/<id>.json`

- **Save preset**, **Confirm overwrite** (keeps a backup), or **Save as new**.
- **Refresh preset list > Saved preset > Load selected preset** to load.
- To load a preset onto a shared mesh, edit a vertex first to make it independent.
- Invalid presets and files larger than **2 MiB** are rejected.

## Configuration

`BepInEx/config/sprocket.advancedaps.cfg` is created on first launch. Close the game before editing.

- Global enable and debug logging
- Detection and reaction multipliers
- Per-module enable, range, reaction and interception multipliers
- Hard-kill projectile speed limits
- `Audio/LaunchVolume` (default `0.8`; `0` = mute)

Example: `ConfigExamples/sprocket.advancedaps.cfg`. Do not overwrite your customized config.

## Limitations

- HUD and F7 coverage visualization are not implemented.
- Some models are placeholders; Custom APS behavior still needs further in-game testing.

## Troubleshooting

Check `BepInEx/LogOutput.log` for `[SAAPS]`. Set `DebugMode = true` in the config for engagement logs. If parts are missing, verify the game/API versions and both extracted folders.

## Build from source

Requires **.NET 6 SDK**, game/mod assemblies and the [v1.0.1 tagged source](https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection/tree/1.0.1).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1  # Build ZIP + SHA256 in dist/
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1  # Install local build (game closed)
```

## License

MIT — see [LICENSE](LICENSE).
