# Verification Status

## SAAPS 1.0.2 — 2026-10-11

Build references: local Sprocket 0.2.55.5 / Unity 6000.3.21f1, BepInEx 6.0.0-be.788 IL2CPP, .NET 6 SDK. ModAPI is not a compile-time dependency. Current fixes are not installed or published as a Release.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File release.ps1` for offline checks, no-deploy Release build and ZIP entry/hash verification:

- Metadata: version/author/repository/MIT alignment, retained copyright, optional ModAPI and exact game/loader `sp-compat` draft.
- Config: shipped defaults match production bindings.
- Parts: stable IDs/GUIDs, native Custom schema, geometry/UV/normals/material bounds, 8,000-triangle ceiling, localization/icons/previews and icon fill RGB >=150.
- RegistryCheck: production interception/cone rules, settings/schema/geometry bounds, Unicode/locale, atomic preset backup, settings-only index and WAV decoding. Despite its historical name, not registry CI.
- ModuleCheck: production module reload, health/disposal, zero probability and settings-state retention with Unity stand-ins. Native gameplay is not exercised.
- MenuCheck: production grouping, Custom placement callback, editor lifecycle and stale-context guards with stand-ins.
- EngagementCheck: production classifier/planner/hard-kill/soft-kill/manager paths; CannonHE zero, Hybrid fallback sharing ammo/cooldown, no jam after release, failed jam cooldown, Custom manager exclusion and minimum stand-off. Native release uses a fake register; Slerp uses a simplified test substitute.
- Optional `tests/check-inspector.ps1`: compiled tooltip-construction inspection via local Mono.Cecil. Not a packaging prerequisite or clean-checkout dependency.

Offline PASS is not native rendering/gameplay acceptance or a profiler result. Prior installed DLL hashes apply to the older menu/tooltip revision, not 1.0.2.

## Remaining acceptance

1. Menu contains eleven APS options only under Firepower. Place/edit Custom; marker never applies to ordinary plates. Verify inspector input and stale callbacks.
2. Preset save/confirmed overwrite/backup, load on another vehicle, invalid input refusal and shared-mesh isolation. Native packed/rivet geometry validation and undo remain native-owned; if apply and rollback both fail, do not overwrite original saves.
3. Vehicle save, quit/restart/load without external preset; duplicate/mirror/rebuild preserve settings and identity without resetting ammo/health/timers.
4. Two vehicles with distinct camo; repaint/save-load without shared recolor. Shtora glass stays red/glossy while housing takes camo. Seven photo-reference drafts are not measured replicas; Drozd-2, Arena-M and Zaslin remain placeholders.
5. Listen to synthetic hard-kill sounds on miss/success; check mute/distance/cleanup. Soft-kill stays silent. Native tracer and 40 mm effect require live confirmation; explosion follows verified native release only. Historical tracer/audio errors do not constitute acceptance of current fixes.
6. Guided jam cone/range, disable/destroy/release cleanup, Hybrid miss fallback and one ammo/cooldown per attempt. Built-in jam full angles 30/20 degrees; Afganit hard-kill 350/65. Custom minimum stand-off remains strict.
7. Config reload with non-finite/out-of-range values and invalid keys, optional Harmony patch failure, unload/scene cleanup. Measure projectile-heavy FPS/allocations; idle gating is not a measured performance claim.

HUD/F7 coverage remain unimplemented. Profile JSON is reference data, not preset format. Top-attack/UAV behavior requires corresponding native projectiles.

Registry auto-install remains blocked: required game-root parts/localization have no supported modfile supplier mapping. Local `sprocket-mod.json` no longer advertises invalid registry types or DLL-only rules. Manual full ZIP only; no upstream CI PASS claimed. Compatibility is pinned to 0.2.55.5 and 6.0.0-be.788 in the Release-body draft; no remote Release/tag changed.

MIT metadata matches LICENSE, not account equivalence or clearance of third-party material. Preserve separate rights/provenance; reference access does not grant redistribution. No game assemblies, reference downloads, local logs, user configs or presets are packaged.
