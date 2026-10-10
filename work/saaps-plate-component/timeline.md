# Timeline (append-only)

## 2026-10-09T23:09:41.1839881+07:00 | lead | init
- action: case-init
- command_or_ref: skills/scripts/case-init.ps1
- result_summary: case directory created; scope ready_for_act=true
- artifacts: [scope.md, workitems.md]
- evidence_ids: []
- decision_delta: [case_initialized]
- carry_forward_refs: [scope.md]
- next: open PRIMARY SKILL.md and ACT within scope
## 2026-10-09T23:12:00+07:00 | lead | dynamic-recheck
- action: Triaged supplied runtime log; confirmed PlateStructure.TryCast/Blueprint is unavailable for the logged component wrapper. Compared CarouselAutoloader exact owning VehicleObject.GUID check; changed SAAPS exact GUID registry and diagnostic, validated, rebuilt, reinstalled.
- command_or_ref: user runtime log; refs/CarouselAutoloader/BustleRuntime.cs:39-53; SAAPS/src/SAAPS/Parts/APSParts.cs; SAAPS/src/SAAPS/Main.cs; tests/check-parts.ps1; build.ps1; install.ps1
- result_summary: Part checks pass (10); Release build passes (0 warnings/errors); installed DLL hash matches build (BEEF8719D9714BC14E5CE614A6C04B1BC...); only SAAPS DLL and APS part/localization files installed. Awaiting fresh game log with vehicleObjectGuid diagnostic to confirm module recognition.
- artifacts: [../../src/SAAPS/Parts/APSParts.cs, ../../src/SAAPS/Main.cs, ../../tests/check-parts.ps1, ../../generated]
- evidence_ids: [E-runtime-only]
- decision_delta: [Recognition switched from unavailable plate blueprint name to owning VehicleObject.GUID, matching the working autoloader pattern]
- carry_forward_refs: [scope.md]
- next: Restart game; verify vehicleObjectGuid maps to APS part GUID and scan count is nonzero, then test actual interception.
## 2026-10-09T23:19:00+07:00 | lead | native-part-identity-fix
- action: Used supplied runtime evidence to reject VehicleObject.GUID matching (logged values are per-placed-object random GUIDs). Followed ThermalSight native part database lookup: resolve component.ComponentID against active PartInfoSource.partDatabase component fileIDs, then map matching APS PartDefinition.guid to APS definition; cache successful wrapper lookups and clear on release. Rebuilt and installed SAAPS only.
- command_or_ref: user log; refs/ThermalSight/ThermalEra.cs:15-36; tests/check-parts.ps1; build.ps1 -GameRoot D:\SteamLibrary\steamapps\common\Sprocket -Configuration Release -SkipDeploy; install.ps1 -GameRoot D:\SteamLibrary\steamapps\common\Sprocket
- result_summary: APS part check passes; Release build passes 0 warnings/errors; installed DLL SHA-256 matches build (6EC1A1F25759C7E07B91B7A28B0727EF6...); no other mod files modified. Native component database lookup now uses the same source/pattern as Thermal and exact unique SAAPS component fileID saaps_<system>.
- artifacts: [../../src/SAAPS/Parts/APSParts.cs, ../../src/SAAPS/Main.cs, ../../tests/check-parts.ps1, ../../generated]
- evidence_ids: [E-runtime-only]
- decision_delta: [Rejected placed-object GUID matching; switched to active part catalog mapping from runtime ComponentID to catalog PartDefinition GUID]
- carry_forward_refs: [scope.md]
- next: Restart game and verify vehicle scan returns APS module(s); then test projectile intercept behavior.