# Sprocket Advanced APS (SAAPS)

**Version:** 1.0.0
**Game:** Sprocket 0.2.55.5
**Target:** Unity 6000.3.21f1, IL2CPP, Windows x64
**Loader:** Sprocket Mod Loader (BepInEx 6 IL2CPP)

Sprocket Advanced APS adds a complete Active Protection System simulation to Sprocket. It provides ten historical APS from the USSR/Russia, Ukraine, Israel, and Germany, each with its own detection envelope, engagement rules, countermeasures, and visual identity.

## Features

- **10 APS systems**, each with distinct detection, engagement, and countermeasure behavior:
  - **Drozd-1** (USSR, hard-kill) - forward-arc protection, finite interceptors
  - **Drozd-2** (USSR/Russia, hard-kill) - improved coverage and target management
  - **Arena** (Russia, hard-kill) - multi-direction radar, threat discrimination
  - **Arena-M** (Russia, hard-kill) - modular, flexible angles, limited top-attack
  - **Afganit** (Russia, hybrid) - hard-kill + soft-kill, threat classification
  - **Shtora-1** (USSR/Russia, soft-kill) - EO dazzler, laser warning, no interceptors
  - **Trophy / Windbreaker** (Israel, hard-kill) - multi-radar, flat interceptors
  - **Iron Fist** (Israel, hard-kill) - radar + EO, proximity-fused interceptors
  - **AMAP-ADS / StrikeShield** (Germany, modular hard-kill) - per-zone modules
  - **Zaslin** (Ukraine, hard-kill) - external modules, limited-use charges

- **Real threat detection** using the native Sprocket `ProjectileRegister`.
- **Threat classification** distinguishes RPG, ATGM (SACLOS/MCLOS/fire-and-forget), HEAT, HE, APFSDS, cannon HE, UAV/FPV, and loitering munitions.
- **Hard-kill interception** physically removes intercepted projectiles from the native register so collision and damage stay consistent.
- **Soft-kill jamming** disrupts guided missile guidance with time-varying noise proportional to guidance type.
- **Per-module state**: health, operational status, countermeasure capacity, cooldown, destroyed state.
- **Vehicle Designer integration**: APS parts appear in the editor with mass, cost, and an inspector showing coverage, threats, and status.
- **Coverage visualization** (F7) showing detection/engagement sectors and blind spots.
- **Runtime HUD** showing APS ONLINE/OFFLINE, countermeasure remaining, and threat tracking.
- **Save/Load**: APS configuration persists with the vehicle blueprint.
- **Balance modes**: Realism, Balanced, Sandbox.
- **JSON-driven profiles**: add custom APS presets without recompiling.
- **Optional mod integration** with Shell Selector, Material Selector, Thermal Sight, Cold War Expansion, Carousel Autoloader, Hydropneumatic, SmokeSuppressor, and Keybinds.

## Balance disclaimer

All effectiveness values are **gameplay approximations**, not confirmed real-world performance. The Afganit APFSDS capability in particular is a simulated feature based on public claims and is explicitly flagged as such in the inspector and config.

## Requirements

- Sprocket 0.2.55.5
- BepInEx 6.0.0-be.788 (IL2CPP)
- Sprocket Mod API >= 1.1.0, < 2.0.0 (required)
- .NET 6 runtime (provided by BepInEx)

## Installation

1. Install BepInEx 6 IL2CPP for Sprocket.
2. Install Sprocket Mod API.
3. Extract the `SprocketAdvancedAPS` release ZIP into the Sprocket install directory (next to `Sprocket.exe`).
4. Launch the game. Open **Firepower > Active Protection Systems**. APS uses native `cannon`/`scope` palette tags, with SAAPS-only menu grouping; it does not join Thermal's Sights and electronics group.

For a local build, run `powershell -File tests/check-parts.ps1`, `powershell -File build.ps1 -SkipDeploy`, then `powershell -File install.ps1`. The installer deploys the DLL, ten part definitions, localization, ten custom mesh files, and ten catalog icons (`plugins/SprocketAdvancedAPS/icons/saaps_<id>.png`). The installer also deploys the shared surface texture atlas. Icons are generated from each part's UV-mapped mesh and atlas and wired into card icon hooks for exact SAAPS identifiers and GUIDs only; in-game catalog appearance still needs live acceptance.

**Migration:** remove and replace APS previously placed as add-on structures. Those saved structures do not gain the new `saaps_<system>` model component IDs from a DLL update. New parts retain their own identity instead of treating ordinary vehicle compartments as APS.

Offline registry regression: `dotnet run --project tests/RegistryCheck.csproj -- generated/Sprocket_Data/StreamingAssets/Parts`. This checks generated IDs against the production registry; it does not verify native placement or interception. Live acceptance requires a nonzero module initialization log, followed by an observed successful interception.

See **INSTALL.txt** for the full layout.

## Usage

- In the Vehicle Designer, add APS radar, launcher, and sensor parts to your turret.
- Press **F7** to toggle coverage visualization.
- In battle, the HUD shows APS status in the top-left corner.
- APS activates automatically when a qualifying threat enters the engagement envelope.

## Configuration

Edit `BepInEx/config/sprocket.advancedaps.cfg` to change:
- Global enable/disable
- Detection and reaction multipliers
- HUD visibility
- Debug mode
- Keybinds (HUD toggle: H, Debug toggle: F12)

Hard-kill/hybrid sections (`Modules.<id>`) also provide `SpeedBasedInterception`, `MinInterceptSpeed`, and `MaxInterceptSpeed`. Speed mode defaults on: Drozd-1 defaults to **50–600 m/s**; other systems retain their existing maximum and use a minimum no higher than 50 m/s. Values are gameplay approximations. Disabling speed mode restores the definition's class and speed filters.

Speed mode bypasses threat-class eligibility, not ownership, incoming trajectory, coverage, reaction time, health, ammo or cooldown. Class still determines probability. Explicit zero class probabilities use the class fallback in speed mode (slow APFSDS: 10%); `InterceptMultiplier = 0` still disables successful interception. Radar detection starts the reaction timer, but a target whose predicted impact lands inside the reaction delay is engaged immediately — without that, a 200 m/s ATGM leaves a ~40 ms window while the fastest module still needs 30 ms of tracker reaction, so those shots were never fired. A round already inside a module's minimum stand-off is also engaged on direction alone: the range floor exists to keep the launcher clear of its own burst, not to let a 3.5 m HEAT round pass unopposed. Firing still requires the current projectile inside the module's engagement envelope. Native release must be observed before declaring live interception verified.

Capacity is per placed module: Drozd-1 4, Drozd-2 6, Arena 8, Arena-M 10, Afganit 12, Trophy 8, Iron Fist 8, AMAP-ADS 4, Zaslin 2. Each attempt consumes one interceptor, including misses. Reloading is deliberately slow and refills a spent module to full capacity: Iron Fist 30 s, Shtora-1 40 s, Arena-M 45 s, AMAP-ADS 60 s. Other hard-kill definitions do not reload.

Module selection separates the two capabilities. Hard-kill systems compete on intercept probability; a soft-kill system carries none, so it competes on jam probability instead and a free hard-kill module still wins and removes the round outright. Simultaneous-engagement slots go to a module that can fire right now: a round already inside a module's engagement envelope outranks one still inbound, because scarce slots spent on a target that cannot be shot this tick starve the rounds that can be.

Custom APS presets can be defined in JSON in the mod directory (see `Config/default-aps-profiles.json`).

## Compatibility

SAAPS is designed to coexist with the reference mod ecosystem. Optional integrations:

| Mod | Integration |
| --- | --- |
| Shell Selector | Reads ATGM/SACLOS/MCLOS/APFSDS/HEAT classification for interception |
| Material Selector | APS does not alter armor, mass, cost, or protection calcs |
| Thermal Sight | APS warnings are independent of thermal profile |
| Cold War Expansion | APS respects era and custom ammunition |
| Carousel Autoloader | APS countermeasures are a separate resource from cannon ammo |
| Hydropneumatic | APS modules follow suspension transform |
| SmokeSuppressor | APS VFX are tagged distinctly so SmokeSuppressor can hide engine/muzzle smoke without hiding APS effects |
| Quality of Life | APS avoids conflicting hotkeys; uses its own keybinds |

## Interception visual

A confirmed kill plays the game's own projectile explosion effect at the intercept point, requested through the register's native `ShellImpactMarkEffectFactory` (`ProjectileEffectType.Explosion`, no added blast damage or spall), matching how Shell Selector shows APHE detonations. It is requested only after `ProjectileRegister.Release` is verified, so a miss, a rejected shot or a failed release shows no explosion, and a failing effect never changes the intercept result. Line trails use one cached material per colour instead of a new material per shot.

Native explosion scale follows the `Calibre` field, so the burst is requested as a 40 mm interceptor detonation rather than the incoming round's own calibre.

## Soft-kill

Soft-kill and hybrid systems (Shtora-1, Afganit) jam guided rounds; pure hard-kill systems do not jam. The native projectile instance carries no guidance state — only position, velocity and flags — so jamming is modelled the way real soft-kill behaves: while the jam holds, the round is walked off its aim line until SACLOS/MCLOS loses the lock, at constant speed, and the round remains a real projectile that will miss. Jamming never steers a round back toward the vehicle, and never changes its speed.

A jam consumes one countermeasure and is dispatched by the planner. Shtora-1 has 20 uses with a 2 s refill. Duration scales with the module's reaction time plus 3 s. A hybrid system launches its interceptor first and only jams a round that survives.

## Performance

- Native active projectiles are snapshotted once per physics tick and shared by all vehicles; sensors read that snapshot instead of re-enumerating the IL2CPP `activeIdMap` per vehicle.
- The tracking/planning core runs once per simulation tick. Render frames only recheck launches that are already assigned.
- Vehicle transform and rigidbody velocity are read once per vehicle per tick and cached (the body is only re-resolved if a previously found one is destroyed).
- Detection is throttled to a 20 ms simulation-time interval. Effective timing still depends on the game tick/frame rate; a short interception window can be skipped at high speed.
- Repeated per-threat log lines are throttled (5 s for rejections, 2 s per decision), and native read failures warn at most every 5 s.
- Set `General/PerformanceLog` to `true` to log mean physics-core time and mean snapshot native reads every 5 s: `[SAAPS] PERF physicsMean=...ms snapshotReadsMean=... samples=...`. It measures the SAAPS physics core, not total frame time; compare before/after in the same scene with the same vehicle and projectile count.

## Known limitations

- Local builds use installed .NET 6 and Sprocket assemblies. Runtime logs confirm native APS model identity, menu grouping, threat detection, and released native projectiles (`INTERCEPT SUCCESS`). The explosion visual, catalog icons and the lag reduction still need live acceptance; offline checks do not prove in-game appearance or frame-time change. See **VERIFICATION.md** for older feature status notes.
- Models use generated UV-mapped geometry and an original 512×512 surface atlas (`plugins/SprocketAdvancedAPS/textures/saaps_surfaces.png`). Three SAAPS-owned HDRP materials distinguish olive/sand paint, dark metal and sensor faces; no third-party models/textures are redistributed. Larger tubes have smooth normals and housings have stepped edge bevels. Missing textured surfaces warn and retain native materials. Drozd-1, Shtora-1 and Trophy now have draft individual equipment assemblies based on local reference photographs, with front/side/isometric previews in `generated/previews/`. Seven other systems still have approximate placeholder layouts. Mesh validity checks do not prove photographic likeness or in-game appearance. See `work/model-reference-notes.md` for references, modeling estimates and remaining uncertainties. Part IDs and gameplay capacity remain unchanged; a visible equipment assembly is not the entire vehicle-wide APS installation.
- Top-attack and UAV/FPV interception depend on those objects existing in the scene as projectiles.

## License

GPL-3.0-only. See **LICENSE** for details.

## Credits

See **CREDITS.md** for asset and reference credits.
