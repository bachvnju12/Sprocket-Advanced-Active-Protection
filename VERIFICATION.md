# Verification Status

This document records what was verified and what was not.

## Environment limitations

This development environment does **not** include:
- The .NET SDK (no `dotnet` command available)
- The Sprocket game installation
- The BepInEx / Unity interop assemblies
- The SprocketModAPI assembly

Because of this, the following were **not** performed here:
- `dotnet build` compilation
- Automated contract tests
- In-game runtime validation
- FPS/performance measurement

The source is structured to compile against Sprocket 0.2.55.5
with BepInEx 6 IL2CPP and Sprocket Mod API 1.1.x. Build and
in-game verification must be run on a machine with the game,
BepInEx, Sprocket Mod API, and the .NET 6 SDK installed.

## Verification matrix

| Feature | Static | Build | In-game |
| --- | --- | --- | --- |
| APS definitions (10 systems) | Reviewed | Not run | Not run |
| Part JSON schema | Reviewed | Not run | Not run |
| Threat classification logic | Reviewed | Not run | Not run |
| Threat detection (ProjectileRegister) | Reviewed | Not run | Not run |
| Hard-kill interception (Release) | Reviewed | Not run | Not run |
| Soft-kill jamming (velocity noise) | Reviewed | Not run | Not run |
| Per-module state machine | Reviewed | Not run | Not run |
| Coverage sectors / FOV | Reviewed | Not run | Not run |
| Countermeasure capacity/cooldown | Reviewed | Not run | Not run |
| Vehicle Designer inspector | Reviewed | Not run | Not run |
| Coverage visualization (F7) | Reviewed | Not run | Not run |
| Runtime HUD | Reviewed | Not run | Not run |
| Save/Load persistence | Reviewed | Not run | Not run |
| Balance modes | Reviewed | Not run | Not run |
| JSON preset loading | Reviewed | Not run | Not run |
| Diagnostics report | Reviewed | Not run | Not run |
| Mod integration hooks | Reviewed | Not run | Not run |

## Required in-game test scenarios

When run on a real installation, verify:

1. Vehicle with no APS - no HUD, no engagements.
2. Vehicle with one APS - detection and engagement in arc.
3. Vehicle with multiple same-type modules - more coverage.
4. Vehicle with multiple APS types - combined behavior.
5. Projectile flying past without threat class - no engagement.
6. RPG flying directly at vehicle - hard-kill intercept.
7. SACLOS ATGM - soft-kill jam (Shtora) or hard-kill.
8. APFSDS - not engaged by most APS (Afganit simulated only).
9. Two missiles simultaneously - multi-engagement handling.
10. Multiple projectiles from multiple angles - sector selection.
11. Top-attack (Arena-M with module placement).
12. Module out of countermeasures - no engagement, HUD shows 0.
13. Module destroyed - no engagement, HUD shows DESTROYED.
14. Vehicle moving - prediction accounts for motion.
15. Turret rotating - coverage follows turret.
16. Suspension change - modules follow transform.
17. Save and reload blueprint - APS config restored.
18. Delete module in editor - state cleaned up.
19. Restart game - no duplicate parts.
20. Shell Selector installed and not installed.
21. SmokeSuppressor on and off.
22. Without optional dependencies - mod still loads.

## Performance measurement

Run with the in-game profiler and record:
- One vehicle, one APS, one projectile.
- Multiple vehicles, multiple APS, multiple projectiles.
- Multiple simultaneous interceptions.

Do not report FPS numbers until they are measured on real hardware.

## How to build and test

On a machine with the game and SDK:

```
cd src/SAAPS
dotnet build --configuration Release -p:SprocketGameRoot="D:\Games\Sprocket"
```

Then run the diagnostics from the in-game console or the
mod menu to confirm load, and exercise the scenarios above.

## Known gaps

- Real 3D meshes are not bundled; the part JSON references
  mesh GUIDs that must be supplied as licensed assets.
- Top-attack and UAV/FPV interception depend on those objects
  existing as projectiles in the scene.
- The custom JSON parser in APSPersistence is minimal; a full
  JSON library would be more robust.
