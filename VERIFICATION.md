# Verification Status

## 2026-10-10 release preparation

Environment: .NET 6 SDK, installed Sprocket 0.2.55.5 / Unity 6000.3.21f1,
BepInEx 6 IL2CPP and Sprocket Mod API assemblies available locally.

- Release build: PASS, 0 warnings, 0 errors.
- `tests/RegistryCheck.csproj`: PASS; production registry and pure gameplay helpers.
- `tests/check-parts.ps1`: PASS; ten persistent model IDs, GUIDs, native exterior
  material reference, winding, UVs, normals, valid material slot indices, nondegenerate
  triangles, <=8,000 triangles/module and icon filled RGB >=150. Current generator
  writes front/side/isometric previews for Drozd-1, Shtora-1 and Trophy only.
- Native paint/factory signatures: inspected through managed metadata only.
- Installed 43 SAAPS files: SHA256 matched local build/generated sources.
- `release.ps1` checks each ZIP entry against staging bytes and writes checksums.

Offline checks do not prove in-game appearance, native behavior or performance.

## Current limitations

- Camo/repaint and per-vehicle native material ownership need live validation.
- Native shell tracer adapter exists; APFSDS-style appearance is not live verified.
  Prototype collider/rigidbody presence disables tracer; visual failure must not
  prevent native interception. No Launch or projectile register entry is added.
- Soft-kill has no beam/tracer/explosion. Cone math is tested at +/-15 degrees yaw,
  +/-10 degrees pitch, outside boundaries, invalid input and inverse rotation.
- Afganit hard-kill remains 350/65 degrees; jam gate is separate 30/20 degrees.
  Existing shared hybrid cooldown can block jam following an interceptor launch.
- HUD, F7 coverage and designer inspector are stubs, not implemented.
- JSON profile file is reference data; runtime uses compiled definitions and .cfg.
- Three reference-based meshes remain simplified drafts; seven layouts remain
  approximate placeholders. Geometry checks do not assess photographic likeness.
- Top-attack/UAV behavior requires those objects to exist as native projectiles.

## Required live checks

1. Two vehicles with distinct paint/camo; repaint after placing APS; duplicate,
   save/load and check one vehicle does not recolor another.
2. Hard-kill launch/miss/success: short shell tracer, no yellow beam; native 40 mm
   explosion only after verified release, no added blast damage/spall.
3. Soft-kill emitter rotation: jam guided hostile rounds only within +/-15 yaw and
   +/-10 pitch and configured range; stop when leaving cone, disabling/destroying
   emitter or releasing vehicle. Preserve projectile speed and incoming tracer.
4. Afganit hard-kill outside jam cone remains eligible. Check shared cooldown.
5. Module identity/placement after save/load, finite ammo/cooldown, own-round
   rejection and scene/unload cleanup.
6. Measure actual performance before making FPS/latency claims.

## Reproduce offline checks

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1
```

No game deployment during release packaging. No game assemblies, reference photos,
local probes, logs or user config are distributed in the ZIP.
