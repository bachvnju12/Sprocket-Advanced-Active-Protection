# SAAPS release notes

<!-- sp-compat {"hamish.sprocket": ">=0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

## Metadata correction

- Author/maintainer metadata: `bachvnju12`.
- Repository: https://github.com/bachvnju12/Sprocket-Advanced-Active-Protection.
- Project license declaration: `MIT`, matching the existing `LICENSE` file. Its original copyright notice is retained.
- Required mod dependency: registered package `furryaxw.sprocket-mod-api`, version `>=1.1.0 <2.0.0`, applying to all SAAPS versions (`when: "*"`).

## Development changes — live acceptance pending

- Custom APS editable plate, Unicode names, per-part settings and geometry/settings presets with confirmed overwrite and `.bak` backup.
- Seven exterior model drafts revised from photographs; Drozd-2, Arena-M and Zaslin remain placeholders. Models are not measured replicas.
- Three original synthetic hard-kill launch sounds, spatial attenuation and `Audio/LaunchVolume`; soft-kill stays silent. These are not real APS recordings.
- Fixed soft-kill dispatch gate and custom reload after destruction; settings refresh preserves ammo/health/cooldown.

Offline checks do not verify native placement, duplicate/mirror, vehicle persistence, paint/camo or audible playback. Do not present this development build as live-tested.

The `sp-compat` comment is for the GitHub Release **body**, not only an attached file. Publishing or changing an existing release is a separate action. Game range follows the requested minimum declaration; local API/build checks cover 0.2.55.5 only, not every later game version.

## Registry blockers still requiring acceptance

The existing registry manifest's `sprocket:parts`, `sprocket:config` and `sprocket:assets` install types are not supplied by the registered BepInEx loader, which supplies only `bepinex:core`, `bepinex:plugin` and `bepinex:patchers`. A DLL-only registry download cannot install required part definitions/assets. Modfile rules target supplier directories, not arbitrary game-root paths; relabelling SAAPS as a loader/patch is not a fix. Unverified optional recommendation IDs were removed. Do not claim registry readiness until the full ZIP layout has a supported install strategy and registry CI passes.

References: [CONTRIBUTING.en.md](https://github.com/furryaxw/SprocketModManager/blob/registry/CONTRIBUTING.en.md#the-compatibility-declaration), [schema](https://raw.githubusercontent.com/furryaxw/SprocketModManager/registry/schemas/sprocket-mod.schema.json), [BepInEx supply](https://raw.githubusercontent.com/furryaxw/SprocketModManager/registry/mods/bepinex/bepinex-be.json).

Third-party license names in credits are not changed to MIT. Reading reference code is not proof that no protected code was copied; any copied GPL material would need a license audit before MIT distribution.
