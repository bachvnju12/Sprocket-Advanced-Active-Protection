# Changelog

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
