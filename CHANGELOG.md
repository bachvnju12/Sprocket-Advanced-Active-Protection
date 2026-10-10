# Changelog

## 1.0.0 packaging update - 2026-10-10

- Persistent model IDs, generated meshes/icons/atlas and native exterior paint adapter.
- Selective mechanical details across ten models; three reference drafts, seven approximate layouts.
- Native shell visual adapter replaces yellow hard-kill line; 40 mm explosion remains release-gated.
- Soft-kill beam removed; separate 30/20-degree jam cone rechecked during held jams.
- Clean runtime .cfg template, config-default regression, no-deploy release script and verified ZIP/checksums.
- Added Git ignore rules for local build/probe/release output.
- Corrected installation and verification docs. HUD/F7/inspector, balance-mode UI and JSON preset loading are not implemented; older entries below describe intended features, not verified completion.
- Camo lifecycle, native tracer appearance and current model quality remain live acceptance items.

## [1.0.0] - 2026-10-09

### Added
- Initial release of Sprocket Advanced APS.
- 10 APS systems: Drozd-1, Drozd-2, Arena, Arena-M, Afganit, Shtora-1, Trophy, Iron Fist, AMAP-ADS, Zaslin.
- Threat detection via native ProjectileRegister.
- Threat classification: RPG, ATGM (SACLOS/MCLOS/fire-and-forget), HEAT, HE, APFSDS, cannon HE, UAV/FPV, loitering munition.
- Hard-kill interception that removes projectiles from the native register.
- Soft-kill jamming with guidance-type-specific noise.
- Per-module state: health, operational status, countermeasure capacity, cooldown, destroyed state.
- Vehicle Designer integration: APS parts with mass, cost, and inspector.
- Coverage visualization (F7).
- Runtime HUD.
- Save/Load of APS configuration with vehicle blueprints.
- Balance modes: Realism, Balanced, Sandbox.
- JSON-driven profiles for custom presets.
- Optional mod integration hooks.
- Diagnostics and automated test report.
