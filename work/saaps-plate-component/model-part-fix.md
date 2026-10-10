# SAAPS model-part identity change — 2026-10-09

## Evidence and correction

User logs show zero APS modules on both vehicles. The displayed `plateStructure` components have GUID `7f8a9d20-eb45-482e-b149-014c964c4e2c`, identified in reference source as CompartmentGuid. They are not evidence of an APS component. Earlier claims that these GUIDs were random or these displayed compartments were APS were unsupported.

The previous catalog fallback compared generic runtime IDs with custom APS fileIDs and did not solve missing identity. Removed catalog scans and pointer cache.

Reference `ThermalSight/parts/sightConsolePart.json` uses a native `model` component with a custom fileID, a normal transform, and native mesh/material GUIDs. The native bedRolls definition provides the fallback model resources and Crew Equipment palette tags used here. SAAPS now generates ten normal model parts with exact `saaps_<system>` fileIDs; its own model hooks replace their visuals with generated APS meshes. No native component type or other mod is modified.

Previously placed prefab structures must be replaced manually. Their saved native components do not acquire custom model IDs from the new DLL. No migration heuristic recognizes compartments as APS.

## Checks completed

- `powershell -NoProfile -ExecutionPolicy Bypass -File D:\workspace\sporketmod\SAAPS\tests\check-parts.ps1`: PASS, ten model definitions and distinct meshes.
- `dotnet run --project D:\workspace\sporketmod\SAAPS\tests\RegistryCheck.csproj --configuration Release --no-restore -- <generated or installed Parts directory>`: PASS. Compiles production registry unchanged with offline stand-ins; accepts all ten generated IDs and rejects null, vanilla, unknown, suffixed and case-mismatched IDs. Does not execute native placement.
- Release build with `--no-restore -p:SkipModDeploy=true`: 0 warnings, 0 errors.
- `install.ps1`: installed only SAAPS DLL, parts, localization, metadata and meshes; backed up existing SAAPS files outside the plugin discovery directory.
- Corrected post-install hash verification: all 31 DLL/part/localization/mesh files match sources. An earlier ad-hoc verification incorrectly flattened its first pair; its success text was invalid and superseded by the error-stopping verification.
- Installed DLL SHA256: C97C0E7C46A3C9170DD2AE37ABCAB07BE546BB877175F2C41DCF4C8D331E82A8.
- Backup: D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\SprocketAdvancedAPS-backup-20261009-235032.

## Speed-based interception update — 2026-10-10

- User confirmed new APS placement; runtime logs confirm `saaps_drozd1` model identity, nonzero module initialization and HEAT/ATGM detection. Crew Equipment advice above is superseded: current menu is Firepower > Active Protection Systems.
- Implemented configurable speed-based hard-kill for hard-kill/hybrid modules. Drozd-1 default maximum 600 m/s; minimum defaults no higher than 50 m/s after user reported a 50 m/s ATGM. Class mode retains original definition minimums. Soft-kill-only modules cannot hard-kill.
- Radar no longer owns assignments. Planner retains detection timestamp, prepares incoming targets before range entry and rebuilds assignments by priority. Launch refreshes native projectile state and rechecks speed, trajectory, coverage, reaction, own-shot exclusion, health, ammo and cooldown. Register/ID pairs separate projectile identity and ownership across registers.
- Detection is throttled to 20 ms, checked from render and physics updates. Short high-speed engagement windows can still be skipped by tick/frame timing; no claim of guaranteed interception.
- In speed mode, explicit zero class probability uses fallback (APFSDS 10%). Probability and InterceptMultiplier remain active. Capacity unchanged; attempts including misses consume ammo. Reload definitions refill full capacity, not one round.
- Production rule regression and registry checks PASS; generated part schema PASS. Final Release build: 0 warnings, 0 errors. These tests do not run native projectile release or prove planner behavior in game.
- Installed only SAAPS through install.ps1; all 31 DLL/part/localization/mesh source and target hashes match. Backup: `D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\SprocketAdvancedAPS-backup-20261010-003953`. Installed DLL hash begins `3B6E357C2A32275DEEAB65C8596899D08`.
- Existing SAAPS config was read, not overwritten. New speed entries are bound on next game launch. Other mods and vehicle saves untouched; no subagents used.
- Pending live acceptance: 50 m/s approaching ATGM in correct module angle/range should produce ENGAGE followed by INTERCEPT SUCCESS/MISS. INTERCEPT SUCCESS now requires post-release native absence/inactive flags. Shtora jamming dispatch remains absent; no soft-kill functionality claim.

## Pending live acceptance

Restart game and replace old APS add-on structures with newly generated Crew Equipment model parts. Check `Model ready: component=saaps_...` and `initialized with N APS module(s)` with N > 0. Debug diagnostics now collect component IDs and APS candidates across the entire vehicle, not just the first 32 entries. Then verify actual projectile detection and interception. Neither native placement nor interception was run or confirmed during these offline checks.

## VFX, performance and catalog icons — 2026-10-10

Task: successful-intercept VFX like APHE, reduce lag, replace the white catalog placeholder image. SAAPS source/tests only; ShellSelector and ThermalSight read as references and not modified.

- Live evidence found in the previous session's `BepInEx/LogOutput.log`: `INTERCEPT SUCCESS Arena-M destroyed ATGM_SACLOS` at lines 709, 739, 757, 777 plus a `MISS` at 748. Native projectile release does work in game; earlier "no intercept ever observed" is superseded.
- Success VFX now calls the register's native `Context.ShellImpactMarkEffectFactory.PlayEffect(ProjectileEffectType.Explosion, ...)`, the same visual-only path ShellSelector uses. It is invoked only after `ProjectileRegister.Release` is verified, so misses and failed releases show no explosion. Factory errors are caught, warned once, and never change the release result. No damage, spall, shell impact pipeline or shared `explosionAssets` mutation. Native availability of the explosion visual is not yet observed live.
- The yellow primitive sphere and point light are removed. Trail/jam beams use one cached material per colour, destroyed on unload; audio clips are destroyed on unload.
- Performance: native active projectiles snapshotted once per physics tick, shared by all vehicles (key = register pointer + projectile ID); sensors read the snapshot instead of re-enumerating IL2CPP maps per vehicle; register lookup is direct; vehicle transform/velocity read once per tick with a cached Rigidbody that re-resolves if destroyed; tracking/planning core runs once per tick and render frames only recheck pending launches; designer Build/Release events coalesce into one deferred `SyncModules` that keeps unchanged module ammo/health/cooldown; repeated logs throttled; early range check before classify. Optional `General/PerformanceLog` reports mean physics-core ms and mean snapshot native reads every 5 s.
- Icons: ten PNGs generated from each part's own mesh geometry by `generate-parts.ps1` into `generated/icons`, deployed to `plugins/SprocketAdvancedAPS/icons`. `APSPartIcons` hooks `GameIcons.GetOrDefault`, `PartDefinitionCardFactory.CreateCard/GetAllWithTags/GetByIdentifier` for the exact ten SAAPS identifiers and GUIDs only, caches Sprite/Texture2D, warns once, and destroys everything on unload. `check-parts.ps1` samples pixels and fails on blank/white icons.
- Regression added for native slot liveness (`APSInterceptRules.IsLiveNative`) used by snapshot, Refresh, intercept and soft-kill paths; one flag predicate replaces three duplicated masks. Registry/rules check PASS; part/icon schema check PASS; Release build 0 warnings, 0 errors.
- `install.ps1` deployed DLL, 10 parts, 10 localization, 10 meshes, 10 icons and metadata. Verified 42 source/target SHA256 pairs, 0 mismatches. Installed DLL SHA256 `176A8D07BDD9B4A1DA39C4FFE6399EA37B50FCD2F0CB1F7EC4BFCA1D63C225BE`. Backup `D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\SprocketAdvancedAPS-backup-20261010-102423`.
- Pending live acceptance: explosion visual on success only, non-white catalog icons in-game, and a before/after `PerformanceLog` comparison in the same scene. No claim that lag is gone; the change is a code-level reduction of per-tick native reads and duplicated render-path work.

## Intercept rate and explosion scale — 2026-10-10

### Evidence
LogOutput.log (BepInEx), counted with `.Contains('SAAPS')` (a `-like '*[SAAPS]*'` pattern
matches the wildcard character class and falsely counted "Mission" lines):

    SAAPS lines: 160
    ENGAGE 58 | INTERCEPT SUCCESS 2 | INTERCEPT MISS 0 | INTERCEPT FAILED 0
    NO ENGAGEMENT 40 | RELOAD 0 | PERF 0 | WAIT 0

    ENGAGE ATGM_SACLOS -> Arena-M id=11 speed=200.0m/s limit=1500m/s detectedAgo=0.010s tti=0.03s

### Finding
1. `INTERCEPT MISS` is 0 while `INTERCEPT SUCCESS` is 2. The probability roll in
   `APSHardKillSystem.FireCountermeasure` runs *after* ammo is consumed, so zero misses means the
   roll branch was never reached with a live target: `FireCountermeasure` was almost never entered.
2. `WAIT` is 0 even though the planner has a dedicated WAIT line. It shared `ThreatTrack.NextDecisionLog`
   with the ENGAGE line, which resets the same field every 2 s, so the WAIT branch was always throttled
   away. The log could not distinguish the two gates that block a shot.
3. With `detectedAgo=0.010s` and `tti=0.03s` at 200 m/s the projectile is detected ~8 m out and
   reaches the vehicle in ~40 ms. The fastest module reaction time in the table is 0.03 s (amapads);
   Arena-M is 0.045 s. The engagement window is shorter than the reaction delay, so the reaction gate
   can veto every shot.
4. Explosion size: `refs/ShellSelector/RuntimeShellEffects.cs` passes
   `Calibre = GunDiameter*1000` to the native effect factory. Native explosion scale follows
   `ProjectileEffectInfo.Calibre`; SAAPS passed 35, far below any real munition.

### Path
- `APSInterceptRules.ReactionSatisfied(now, detectedAt, delay, timeToImpact)`: reaction gate passes
  when the delay has elapsed **or** the target reaches the vehicle within the delay. Wired into both
  `APSEngagementPlanner.FixedUpdate` and `APSHardKillSystem.FireCountermeasure` so the two gates agree.
- Planner WAIT log now reports the failing gate (`reason=envelope` vs `reason=reaction`) on a separate
  `NextWaitLog` throttle, so a future low rate is diagnosable from the log.
- `SpawnInterceptExplosion` requests `Calibre = 130`.

### Verification status
- Release build 0 warnings, 0 errors. RegistryCheck PASS (12 ReactionSatisfied assertions incl. the
  fallback cases). check-parts.ps1 PASS.
- Installed with game closed; backup `BepInEx\SprocketAdvancedAPS-backup-20261010-105044`; 40/40
  parts, localization, meshes and icons match by SHA256; DLL `F7243BF5…`.
- NOT verified in game: the new intercept rate, the larger explosion and the per-gate WAIT log all
  need a live combat log before any claim about them.

## Soft-kill SACLOS dispatch and jam model — 2026-10-10

User report: VFX too large (130 mm requested) and soft-kill could not block SACLOS /
gunlauncher-SACLOS ATGM at all.

### Evidence
Metadata probe of `BepInEx/interop/Sprocket.Projectiles.dll` (`_probe/dump`, read-only
reflection, output in `work/probe-*.txt`):

    Sprocket.DamageModelling.ProjectileInstance (base=ValueType)
      prop Transform / ProjectileDefinition def / ProjectileEvent latestEvent / Int32 ID
      prop Vector3 spawnPoint / position / velocity / Int32 layerMask / Single spawnTime
      prop ProjectileFlags flags
      meth Void Update(Vector3 uniformAcceleration)

    Sprocket.DamageModelling.ProjectileFlags: None, Active, Release,
      TerminatedByTerrain, TerminatedByStructure

There is **no guidance/lock state on the native projectile instance** — only position, velocity
and flags. Guidance lives in `ProjectileDefinition.Functions` and is applied inside
`ProjectileFunction.ProjectileUpdate(ProjectileInstance&)`. Jamming can therefore only be
expressed by rewriting velocity, which is what real soft-kill does.

Code inspection of the previous build:

- `APSSoftKillSystem.ApplySoftKill` had **no call site anywhere**; the planner only called
  `APSHardKillSystem.FireCountermeasure`. Soft-kill never ran.
- `APSModuleState.CanPrepare` gated on `APSInterceptRules.Ready(Definition.Type != SoftKill, ...)`,
  so a SoftKill-only system (Shtora-1) could never qualify for any threat.
- `DisruptGuidance` added oscillating noise to the current heading every tick. The round kept
  flying at the vehicle, so it read as shaking, not as a lost lock.
- `_jammedProjectiles` was keyed by `ProjectileId` alone. The native ID counter is per register,
  so two registers could overwrite each other's jam.
- `APSSystems.FixedUpdate` ran the soft-kill pass before the vehicle pass, so a jam dispatched in
  tick N first applied in tick N+1 using a stale vehicle position.

### Path
- `APSInterceptRules.JamReady` drops only the hard-kill veto while keeping enabled/operational/
  health/ammo/cooldown/ownership. `CanPrepare` now accepts hard-kill OR jam eligibility.
- Planner dispatches by type: HardKill -> `FireCountermeasure`; SoftKill -> `ApplySoftKill`;
  Hybrid -> launches first, then jams only a round that survived and still has countermeasures
  left, so one engagement never spends two.
- `ApplySoftKill` now consumes a countermeasure and logs a `SOFT-KILL JAM` line with the rolled
  probability; it was previously neither consuming ammo nor reachable.
- `DisruptGuidance` rewritten: per tick it slerps the heading onto a line that no longer intersects
  the vehicle (`-lineOfSight + sideways*0.6`), holds speed, refuses any write that would leave the
  round heading at the vehicle (`Dot(deflected, lineOfSight) < 0` -> skip), and re-reads the vehicle
  position every tick. `APSInterceptRules.DeflectionRate` / `DeflectionWeight` hold the math and
  clamp a long frame so a dropped tick cannot teleport or reverse the round.
- Jam dictionary keyed by `(register, id)`.
- Soft-kill pass moved after the vehicle pass in `APSSystems.FixedUpdate`.
- `GetNoiseMagnitude` deleted with its only caller.
- Explosion `Calibre` 130 -> 60.

### Verification status
- Release build 0 warnings, 0 errors. RegistryCheck PASS (14 new assertions covering JamReady
  vetoes and deflection clamping). check-parts.ps1 PASS.
- Installed with game closed; backup `BepInEx\SprocketAdvancedAPS-backup-20261010-111842`;
  40/40 parts, localization, meshes and icons match by SHA256; DLL `DE51F220...`.
- NOT verified in game: whether a jammed SACLOS round visibly walks off and misses, the 60 mm
  explosion size, and the hybrid double-action path all need a live combat log.

## Soft-kill never dispatched, slot starvation — 2026-10-10

### Evidence (live log, DLL BD51F220-era install)
32 `ENGAGE` produced 2 `INTERCEPT SUCCESS`, 1 `INTERCEPT MISS`, 0 `SOFT-KILL JAM`,
0 `INTERCEPT FAILED`. Vehicle carried `arenam` + `shtora1` only (status lists in every
`NO ENGAGEMENT` line show exactly those, later plus `drozd1`/`drozd2`).

### Findings
1. Soft-kill had no call path to a threat. `ScoreModuleForThreat` scored modules only by
   `GetInterceptProbability`, and `shtora1` is configured `InterceptProbability[ATGM_SACLOS]=0`
   (it has no interceptors). Its score was ammo-only (~200) against Arena-M's ~850, so the
   planner never assigned a SACLOS round to it and `ApplySoftKill` was never reached. The
   task-C dispatch wiring was correct but unreachable.
2. Simultaneous-engagement slots were spent on rounds that could not be shot. The planner
   assigns at `DetectionRange` (Arena-M 120 m) while `FireCountermeasure` requires
   `CanEngage`, i.e. inside `EngagementRangeMax` (30 m). With 3 Arena-M x
   `MaxSimultaneousEngagements=3`, distant threats held all 9 slots and rounds inside the
   envelope had none. `WAIT ... reason=envelope` at `dist=31.5m` was this, not a tracking bug.
3. Arena-M had `ReloadTime = 0`: 10 rounds ended APS for the rest of the battle. Log ammo
   counts show the drain (`10,10,10 / 9,9,9 / 8`).
4. `EngagementRangeMin = 4` rejected rounds already at 3.5 m. Log `DETECTED` lines show most
   arrivals at 2.1-3.9 m, so the closest rounds were the ones the sector range test refused.

### Changes
- Planner: separate hard-kill and soft-kill candidates. Soft-kill scores on
  `GetSoftKillProbability`; a free hard-kill module still wins and removes the round.
- Planner: a module that can fire right now (threat already inside its envelope) outranks
  one that cannot, so scarce slots are not reserved for unreachable rounds.
- `IsThreatInEngagementEnvelope`: a round inside `EngagementRangeMin` is engaged on
  direction alone, via new pure helper `APSInterceptRules.InsideMinRange`.
- Arena-M `ReloadTime` 0 -> 3 s (refills to capacity once the magazine is spent).
- `NO ENGAGEMENT` log now names type, `eligible=` and class instead of a fixed reason list.

### Verification status
- Release build 0 warnings, 0 errors. RegistryCheck PASS (5 new assertions for
  `InsideMinRange`). check-parts.ps1 PASS.
- Installed with game closed; backup `BepInEx\SprocketAdvancedAPS-backup-20261010-113659`;
  42/42 files match by SHA256; DLL `BDDE13BE19E5D78E0...`.
- NOT verified in game: the new intercept rate, whether a jammed SACLOS round visibly walks
  off and misses, and Arena-M refill behaviour all need a live combat log.

## Round APS bodies and slower reloads — 2026-10-10

Requested: rounder APS models, and reload in the 30-60 s band.

### Changes
- `generate-parts.ps1`: new `Add-Drum` helper builds an upright faceted drum (8/10/12 sides)
  from a barrel wall plus two flat n-gon caps. Every shape in `New-SystemMesh` now uses it
  for its main volumes; small boxes remain only where a square detail is wanted (drozd1 and
  twin top blocks, twin centre rail). Sizes were reduced to match the drum diameters.
- `APSModelAssets.Apply` no longer requires exactly four indices per face; it fans from the
  first index. For a quad this reproduces the previous `(0,2,1),(0,3,2)` triangles exactly,
  and it closes an n-gon cap without overlapping or clipping corners as stacked quads would.
  Minimum face size is now 3.
- `tests/check-parts.ps1` accepts faces of three or more vertices and requires the texture
  slot list to match the vertex count, which is what the loader assumes.
- `Write-Icon` averaged face depth over the actual vertex count rather than a hardcoded 4.
- Reloads moved into the requested band: Shtora-1 2 -> 40 s, Iron Fist 1.5 -> 30 s,
  Arena-M 3 -> 45 s, AMAP-ADS 0.8 -> 60 s. Drozd-1, Drozd-2, Arena, Afganit, Trophy and
  Zaslin keep `ReloadTime = 0` (no reload, as documented).

Reload still refills to full capacity once spent, not round by round. At a 45 s reload a
partially drained Arena-M would otherwise never return to capacity, so this is left as-is
unless the request changes.

### Verification status
- Release build 0 warnings, 0 errors. RegistryCheck PASS. check-parts.ps1 PASS.
- Regenerated meshes: Arena-M 88 verts, 44 quads, 10 n-gon caps, no out-of-range references.
- Icons inspected for `saaps_arenam` and `saaps_drozd1`: drums shade correctly, caps closed,
  no white or blank output.
- Installed with game closed; backup `BepInEx\SprocketAdvancedAPS-backup-20261010-115637`;
  42/42 files match by SHA256; DLL `F47A3405A4477BB5...`.
- NOT verified in game: how the drum meshes render on a placed part (normals and winding at
  the caps), and the new reload timings. Both need a live vehicle.

## Per-system APS model shapes - 2026-10-10

### Request
User asked for a better-looking, more realistic model and pointed at the internet as a
source. Asked which route to take; the answer was generator first, consider downloading after.

### Why not downloaded models
- `scope.md` records `network_profile.mode = offline`; fetching is a new side-effect class
  and needs its own approval before it can happen.
- The Sprocket mesh format takes `vertices`, one triangle list and a single material. No UVs,
  no second submesh. Any downloaded texture would be dropped on load.
- Arena, Shtora, Trophy and Drozd are real hardware; nearly every mesh of them online is
  ripped from a commercial kit (Meng, Trumpeter, Hobby Boss) or a manufacturer's CAD, so
  the licensing is not clean enough to ship inside a GPL plugin.

### Changes (`generate-parts.ps1`)
- New helpers: `Add-RotatedBox` (box on its own base, pitch then yaw) and `Add-LauncherRing`
  (ring of outward-tilted launch tubes).
- `New-SystemMesh` no longer takes a variant index and no longer branches on six shared
  shapes. Each of the ten systems has its own profile; the `$systems` table's `Shape` column
  now names the system, not a shared primitive. The variant stub is gone, so `drozd2` and
  `trophy` no longer render identically, nor do `arena`/`arenam` or
  `shtora1`/`ironfist`/`zaslin`.
- Shapes: Drozd-1 four tubes at 16 deg on a tight ring, Drozd-2 six tubes at 26 deg on a
  wider ring, Arena twelve near-horizontal interceptors around a low disc, Arena-M eight
  modules on the rim of a stepped tower, Afganit two dazzler panels flanking a raked radar,
  Shtora-1 two tall dazzler panels plus a laser-warning mast and no interceptors, Trophy a
  wide flat module with a raked-forward radar plate, Iron Fist a boxy turret over a four-tube
  cluster, AMAP-ADS stacked sensor discs, Zaslin external pods off a low base.
- README "Known limitations" rewritten to describe the per-system shapes.

### Verification status
- Release build 0 warnings, 0 errors. RegistryCheck PASS. check-parts.ps1 PASS.
- Mesh stats, all distinct: afganit 64v/42f, amapads 76v/46f, arena 140v/98f,
  arenam 124v/84f, drozd1 56v/38f, drozd2 88v/60f, ironfist 64v/44f, shtora1 64v/42f,
  trophy 56v/36f, zaslin 80v/50f. No out-of-range face indices.
- All ten icons inspected by eye: distinguishable silhouettes, closed caps, correct shading,
  no white or blank output.
- Installed with the game closed; backup `BepInEx\SprocketAdvancedAPS-backup-20261010-121742`;
  DLL hash matches the build output.
- NOT verified in game: how these read on a placed part at turret scale, and whether the
  tilted tubes and raked panels sit correctly relative to the part's up-face normal. Needs a
  live vehicle. Downloaded models remain unapproved and unused.
