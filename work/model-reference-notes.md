# APS mesh reference pass — 2026-10-10

Status: three reference-based draft meshes and mechanical detail additions to all ten; seven underlying layouts remain approximate placeholders. No game deployment in this pass. Photos remain local reference data, not redistributed assets. Dimensions are modeling estimates, not measured hardware specifications. +Y is up, +Z is the exposed equipment front; placement may require rotation for turret-side mounting.

## Drozd-1

Reference: `ref-images/T-55AD_with_Drozd_active_protection_system.jpg` (T-55AD, right-side cheek installation visible).

Selected assembly: one cheek equipment cluster, not the entire vehicle protection system. Four round canisters in two paired rows, raised equipment housing, rear mounting plate, mounting feet and canister collars. Removed invented round turret base and roof discs. Tube centres and depth are estimated from perspective, not certified dimensions. End caps are recessed; no live round modeled. Precise radar housing profile and mounting arrangement still require more views.

## Shtora-1

Reference: `ref-images/T-90_Shtora-1_infrared_light_emitter.jpg` (close-up).

Selected assembly: one emitter with a thick clipped housing, round front within a rim, front cooling fins, sparse cooling pins on upper/side body, protective upper bar and mounting feet. No second emitter or arbitrary radar tower. Pin density and bracket curvature simplified to keep mesh small. The circular face uses a dark sensor atlas tile, separate from olive paint; no emissive effect is added. HDRP appearance in game is not proven by preview.

## Trophy

Reference: `ref-images/Trophy-APS--Merkava-4M-pic01-Zachi-Evenor__cropped_.jpg` (Merkava side view).

Selected assembly: one side radar panel and adjacent upper launcher/shield assembly in one persistent model. Radar is a vertical clipped rectangle, not a horizontal disc. Launcher has a separate pivot, mounting pedestal and deep flared shielding. Photo partly obscures launcher internals; central block is an approximate covered housing, not a claim about launcher internals. Relative separation and shield profile remain draft pending user visual review.

## Pending layouts

Drozd-2, Arena, Arena-M, Afganit, Iron Fist, AMAP-ADS and Zaslin retain old approximate layouts with selective housing bevels, access panels, sensor faces, straps, feet and bolts. Drozd-1 gains canister clamps and cover lips; Shtora gains layered front rings, rear cover/connectors and reinforced feet; Trophy gains rear radar cover/ribs and launcher pivot/shield reinforcement. No suitable clear local reference set has been inspected for these variants. Do not infer one variant from another or call these photo-matched. Existing mechanical helper corrections (round tubes, radian conversion and hard-edge normals) also affect old meshes, but do not validate their layout.

## Preview limitations

`generated/previews/` contains front, side and isometric PNGs of all ten meshes. Preview uses the runtime reversed triangle fan, backface culling and a per-pixel depth buffer. Normals determine shading; subtle depth edges show recesses. It samples the same UV atlas and interpolated normals used by the meshes. Atlas uses original seeded paint grain: olive/sand paint, dark metal and sensor-face regions. Runtime slot 0 uses native vehicle exterior paint/camo; slots 1/2 use SAAPS HDRP metal/sensor materials. Preview still illustrates fixed olive/sand atlas colors, not live vehicle camo. Catalog icons alone remap shading to keep every filled RGB channel >=150; runtime colors are not brightened. Preview does not simulate HDRP reflections: Unity material, light, shadow, placement and save/load require game checks. Native intercept VFX now requests 40 mm instead of 60 mm; release/success gating is unchanged.

Offline checks: finite bounded coordinates, integer indices, nondegenerate triangles, helper outward winding under rotation, triangle budget, IDs, native exterior material GUID, all three surface slots, localization, all thirty previews and icon shading >=150. None assess likeness automatically.

Visual review of all ten isometric previews found coincident Arena-M housing/rib top faces and an occluded Afganit sensor face; both were adjusted and regenerated. Drozd-1, Shtora and Trophy were compared against the three local photographs above: housing proportions, bracket curves, cooling-pin density and Trophy covered internals remain simplified, not exact replicas. Seven placeholder silhouettes still need proper variant references. Offline registry/cone tests and Release build pass; paint/camo ownership, save/load, native tracer appearance and in-game model quality remain live acceptance items. HUD/F7/inspector are stubs, not rendered cone validation. No game deployment in this pass.
