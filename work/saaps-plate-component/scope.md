# Case Scope

## meta
- case_id: saaps-plate-component
- created: 2026-10-09T23:09:41.1839881+07:00
- operator: local
- project_root: D:\workspace\sporketmod\SAAPS
- primary_skill: reverse-engineering/SKILL.md
- primary_id: R0
- lead_role: lead
- specialist_roles: []
- hint: Use owning VehicleObject GUID to detect custom APS part identity; build and install updated SAAPS; no other mods
- preset: offline-sample

## auth
- status: granted
- basis: own_system
- evidence_of_auth: preset:offline-sample (owner-operated local file)
- MUST NOT proceed if status != granted

## in_scope
- assets:
  - D:\workspace\sporketmod\SAAPS\src\SAAPS\SAAPS.csproj
  - D:\workspace\sporketmod\SAAPS\src\SAAPS\Parts\APSParts.cs
  - D:\workspace\sporketmod\SAAPS\src\SAAPS\Main.cs
  - D:\workspace\sporketmod\SAAPS\generate-parts.ps1
  - D:\workspace\sporketmod\SAAPS\tests\check-parts.ps1
  - D:\SteamLibrary\steamapps\common\Sprocket\Sprocket_Data\StreamingAssets\Parts
  - D:\SteamLibrary\steamapps\common\Sprocket\Sprocket_Data\StreamingAssets\Localization\en-UK\Parts
  - D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\plugins
  - D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\interop\Sprocket.VehicleDesigner.dll
  - D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\interop\Sprocket.PlateStructureDesign.dll
  - D:\SteamLibrary\steamapps\common\Sprocket\BepInEx\interop\Sprocket.Vehicles.dll
- surfaces: []
- activities: []

## out_of_scope
- assets: []
- activities: [dos, phishing_real_users, unrestricted_exfil]

## network_profile
- mode: lab_only
- notes: |
    offline | lab_only | authorized_target_only | unrestricted_lab
    Change mode only after auth.status = granted.
    2026-10-10 user approval: read-only fetch of public Wikimedia Commons reference images
    (upload.wikimedia.org) for APS silhouette reference. No uploads, no other hosts, no tool
    execution inside the download directory.

## deliverables
- report: true
- field_journal: true
- diagrams: true
- timeline: true

## constraints
- timebox: {}
- stealth: low
- data_handling: anonymize

## signoff
- ready_for_act: true
- checklist:
  - [x] auth.status = granted
  - [x] in_scope.assets non-empty OR offline sample path set
  - [x] network_profile.mode chosen
  - [ ] out_of_scope reviewed
  - [ ] roles assigned (see skills/ops/role-map.md)

## ops_refs
- skills/ops/scope-contract.md
- skills/ops/evidence-finding-path.md
- skills/ops/role-map.md
- skills/ops/timeline-workitem.md
- skills/ops/IDENTITY.md