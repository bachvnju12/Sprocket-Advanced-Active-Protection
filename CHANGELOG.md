# Changelog

## Unreleased — 2026-10-11

- Align author/repository metadata with `bachvnju12/Sprocket-Advanced-Active-Protection`; declare MIT to match existing `LICENSE`, preserving its original copyright notice. This is not a completed audit of third-party or previously GPL-labelled material.
- Add release-body `sp-compat` declaration in `RELEASE_NOTES.md` and metadata regression check. Dependency schema now uses `id`, `version`, `when`.
- Add editable Custom APS plate, Unicode names, per-instance settings and versioned shape/settings presets with validation, atomic replacement and backup. Native placement, vehicle persistence, duplicate/mirror and shared-mesh behavior remain acceptance gates.
- Revise seven exterior assemblies from photo references; Drozd-2, Arena-M and Zaslin remain placeholders. Generate all thirty front/side/isometric previews and eleven catalog icons.
- Add three original synthetic PCM launch sounds, bounded WAV decoding, 3D attenuation, voice cleanup and mute/volume config. No verified real firing reference or listening test.
- Fix soft-kill dispatch using its jam cone, dead custom module reload and settings refresh retaining ammo/health/cooldown/reload/operational state.
- Remove unused persistence parser and fabricated PASS diagnostics. Add production preset/audio/module offline checks.
- Build only; no deployment or new public release. Registry multi-directory installation and licensing audit remain unresolved.

## [1.0.0] - 2026-10-10

- Ten persistent APS model parts: Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS and Zaslin.
- Native projectile tracking, hard-kill release, finite countermeasures and cooldowns.
- Guided-round soft-kill uses constant-speed velocity deflection, not a native guidance-state API.
- Separate soft-kill cone: 30 degrees horizontal total, 20 degrees vertical total; live position/range and emitter-state gates. Afganit hard-kill coverage stays separate.
- Native exterior paint adapter and private native shell visual adapter. Paint/camo and tracer appearance remain unverified live.
- Native 40 mm explosion effect requested only after verified projectile release. No soft-kill beam or interceptor visual.
- Generated meshes, UVs, normals, surface atlas and catalog icons. Models remain simplified drafts/placeholders.
- Default config template under `ConfigExamples/` in the release ZIP; existing runtime config is not overwritten.
- Release script runs offline tests, builds without game deployment, packages ZIP with forward-slash paths and verifies SHA256 hashes.
- Short installation README and explicit live-validation limitations.

HUD, F7 coverage, designer inspector rendering, named balance modes and runtime JSON custom-profile loading are not implemented. Offline checks do not prove native runtime behavior or save/load correctness; see `VERIFICATION.md`.
