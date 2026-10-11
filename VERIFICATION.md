# Verification Status

## Current development — 2026-10-11

Environment: .NET 6 SDK; local Sprocket 0.2.55.5 / Unity 6000.3.21f1,
BepInEx 6.0.0-be.788 IL2CPP and Sprocket Mod API reference assemblies.
No deployment or public release of current development changes.

- Release build after settings-state refresh changes: PASS, 0 warnings/errors.
- `tests/check-metadata.ps1`: manifest/project/assembly author, repo and MIT
  declaration match; original copyright notice preserved; requested release-body
  `sp-compat` parses. Dependency shape uses `id/version/when`. Not registry CI or
  a completed licensing audit.
- `tests/RegistryCheck.csproj`: production registry/cone/interception/soft-kill
  helpers, strict custom preset parser/atomic backup and PCM WAV checks PASS.
- `tests/ModuleCheck/ModuleCheck.csproj`: production custom-zero probability,
  reload, destroyed state, jam dispatch, disposal and settings-state retention
  PASS using offline Unity stand-ins. Test subset emits two CS0649 warnings;
  it does not exercise native projectile application or designer lifecycle.
- `tests/check-config.ps1`: 76 defaults match production config bindings.
- `tests/check-parts.ps1`: ten fixed persistent model IDs/GUIDs plus one custom
  plate prefab; winding/UV/normals/material slots/bounds, <=8,000 triangles per
  fixed module, eleven localization/icons and thirty preview dimensions PASS.
  Filled catalog icon RGB channels remain >=150.
- Native signatures inspected using metadata, not assembly execution. Startup
  SerializationInfo round-trip check exists but has not run in the game.
- `release.ps1` checks required custom/audio files, every ZIP entry against its
  staging bytes and SHA256 hashes. Running it packages locally, never deploys.

These checks do not prove native placement, persistence, likeness, audible
playback or performance. Prior installed-file hashes refer to an older build,
not current Custom APS/audio/metadata changes.

## Remaining blockers

- Custom APS marking needs actual catalog-to-new-plate call-chain verification.
  Unmarked ordinary plates must remain ordinary plates. Live owner availability
  during LoadData, duplicate/mirror/rebuild and cache lifetime need verification.
- Native geometry trial ownership/cleanup, packed/rivet metadata validation and
  undo are unresolved. Shared meshes are rejected instead of mutated. Rollback
  failure is reported; do not save over an original vehicle after that warning.
- Inspector active/selection guards compile but stale callbacks need live tests.
- Seven models are photo-reference drafts, not measured replicas. Drozd-2,
  Arena-M and Zaslin remain placeholders. All thirty views were screened in
  contact sheets; full-resolution/reference and game-scale acceptance remain
  pending. Asset schema tests do not measure photographic likeness.
- Camo/repaint and per-vehicle native material ownership need live validation.
- Native shell tracer is visual-only; prototype collider/rigidbody presence
  disables it. No Launch/projectile registration is added. APFSDS appearance
  and 40 mm effect after verified release remain live checks.
- Builtin jam cone is 30/20 full degrees around +Z; Afganit hard-kill remains
  350/65. Shared hybrid ammo/cooldown can block jam after hard-kill launch.
- Audio is original synthetic PCM, not verified real-reference matching.
  SetData playback, miss/success timing, 3D volume and unload cleanup need listening.
- HUD/F7 coverage remain unimplemented. Default profile JSON is reference data,
  not Custom APS preset format. Top-attack/UAV require native projectile objects.
- Registry manifest still selects DLL only and has unverified `sprocket:parts`,
  `sprocket:config`, `sprocket:assets` supply types. CONTRIBUTING describes only
  supplier-directory destinations for modfile; arbitrary game-root payloads
  are not allowed for that kind. Do not relabel SAAPS as a loader/patch to bypass
  this. Full ZIP installation strategy, recommendations and registry CI remain.
- MIT declarations match LICENSE but do not establish account equivalence or
  clear previously GPL-labelled/copied material. Legacy GPL assets are excluded
  from packaging. Third-party/source licensing audit remains required.
- `sp-compat` is local release-body text only. No GitHub Release was edited.
  Requested >=0.2.55.5 range does not mean all later game versions were tested.

## Required live checks after separate installation approval

1. Place Custom APS; verify marker only on returned new plate. Edit vertices,
   edges/faces, Unicode name and settings. Ordinary hull/plate stays non-APS.
2. Save, confirmed overwrite/backup, Save as new, load on another vehicle;
   check invalid/oversized preset refusal and part isolation. Test shared mesh.
3. Save vehicle, quit/restart, remove external preset temporarily, load vehicle;
   verify settings/geometry persist. Duplicate/mirror/rebuild preserve identity
   and settings without refilling ammo or repairing health/timers.
4. Two vehicles with distinct camo; repaint/duplicate/save-load without shared
   recolor. Compare all exterior models at game scale with reference photos.
5. Hard-kill miss/success sound at emitter, volume/mute/distance and voice cleanup;
   soft-kill silent. Short native tracer and 40 mm effect only after verified
   release; no blast damage/spall or extra projectile registrations.
6. Guided-round jam within cone/range; stops on leaving cone or disabling,
   destroying/releasing emitter. Incoming speed/tracer retained. Check hybrid
   hard-kill outside jam cone and shared cooldown.
7. Module finite ammo, own-round rejection, scene/unload cleanup and measured
   performance. Inspect logs for serialization/placement warnings.

## Reproduce offline package checks

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1
```

No game assemblies, reference photos, local probes/logs, runtime config or user
presets are distributed. Packaging is not live acceptance or registry approval.
