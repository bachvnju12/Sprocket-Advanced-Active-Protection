# Credits

## Mod

**Sprocket Advanced APS (SAAPS)** - GPL-3.0-only

## Reference repositories studied

The following open-source Sprocket mods were studied for API
usage patterns, part registration, projectile handling, and
Harmony patch structure:

- Sprocket Mod API (furryaxw/SprocketModAPI) - LGPL-3.0-or-later
- example-mod-bepinex (furryaxw/example-mod-bepinex) - GPL-3.0-only
- Sprocket Shell Selector (RoanWassink/SprocketShellSelector)
- Sprocket Thermal Sight (RoanWassink/SprocketThermalSight)
- Sprocket Hydropneumatic (RoanWassink/SprocketHydropneumatic)
- Sprocket Carousel Autoloader (RoanWassink/SprocketCarouselAutoloader)
- Sprocket Material Selector (RoanWassink/SprocketMaterialSelector)
- Sprocket Cold War Expansion Pack (RoanWassink/SprocketColdWarExpansionPack)
- SmokeSuppressor (furryaxw/SmokeSuppressor)
- Sprocket Quality of Life (Hans21223/Sprocket-Quality-of-Life)

## APS reference data

APS characteristics are based on publicly available information
from Wikipedia and open sources. All effectiveness values in this
mod are **gameplay approximations**, not confirmed real-world
performance. In particular, the Afganit APFSDS interception
capability is a simulated feature based on public claims.

References:
- Wikipedia: Active protection system
  https://en.wikipedia.org/wiki/Active_protection_system

## Assets

The part definitions reference mesh GUIDs and material slots.
The mod does not redistribute third-party 3D models. When
integrating real meshes, ensure the license permits reuse and
redistribution, and record the source and license here.

Procedural meshes included with this release are original works created
for this mod (GPL-3.0-only), not extracted vehicle models. The procedural
512×512 paint/metal/sensor atlas is also original (GPL-3.0-only);
reference photographs are not used as texture pixels.

Visual-reference photographs inspected for the three revised drafts:
- `T-55AD_with_Drozd_active_protection_system.jpg` — Drozd-1 cheek equipment.
- `T-90_Shtora-1_infrared_light_emitter.jpg` — Shtora-1 emitter close-up.
- `Trophy-APS--Merkava-4M-pic01-Zachi-Evenor_(cropped).jpg` — Trophy side installation.

Copies are in `work/ref-images/` for reference only, not shipped assets.
Their original Wikimedia metadata and license details have not yet been
verified in this pass; do not redistribute these photographs as mod assets.
See `work/model-reference-notes.md` for modeling choices and limitations.

## Audio

Fallback audio tones are generated procedurally at runtime.
No third-party audio is redistributed.

## Third-party libraries

- BepInEx 6 (IL2CPP) - provided by the game modding community
- HarmonyLib - provided with BepInEx
- Sprocket Mod API - LGPL-3.0-or-later

All of these are read from the game installation at build time
and are not redistributed with this mod.
