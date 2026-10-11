# SAAPS 1.0.1 — current source / local build

Fixes below are newer than the existing `1.0.1` tag. That Release page has no attached binaries; this document does not update or republish it.

<!-- sp-compat {"hamish.sprocket": "0.2.55.5", "bepinex.bepinex": "6.0.0-be.788"} -->

- APS catalog grouped only under Firepower; native Custom inspector buttons no longer pass a null tooltip.
- HE classification uses CannonHE probability. Hybrid miss can jam the surviving round with the same ammo/cooldown; failed jam attempts also consume ammo/cooldown.
- Custom reload has one countdown path. Minimum stand-off remains enforced; settings edits preserve ammo, health and timers.
- Preset indexing skips mesh validation/cloning; full validation remains on load. Idle vehicles no longer trigger projectile snapshot scans.
- Runtime multiplier validation, safe key reads, throttled driver errors, immediate disposal and audio clip failure latch.
- Optional UI/menu/icon/model patch failures are isolated; startup rollback and unload attempt every cleanup step.
- ModAPI remains optional. Editable Custom plates, presets, model drafts and synthetic launch SFX remain subject to native acceptance.

Offline regression tests use native/Unity stand-ins. They do not prove game rendering, restart/save/load, paint/camo, tracer appearance, audible playback or FPS improvements. Seven models are photo-reference drafts; Drozd-2, Arena-M and Zaslin remain placeholders. Sounds are original synthetic effects, not real APS recordings.

Manual full-ZIP installation only. `sprocket-mod.json` is local package metadata, not a registry entry: required game-root parts/localization lack a supported modfile install mapping. Unsupported `sprocket:*` types and DLL-only download rules were removed. Registry CI/install acceptance remains blocked upstream.

When publishing separately approved, copy this document into the GitHub Release body; the `sp-compat` comment must be in that body. Compatibility pins the locally checked game/loader versions, not all later releases. No Release/tag is changed by building this package.

Registry rules: https://github.com/furryaxw/SprocketModManager/blob/registry/CONTRIBUTING.en.md#the-compatibility-declaration
