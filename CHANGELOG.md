# Changelog

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
