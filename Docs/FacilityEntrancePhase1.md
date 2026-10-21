# Facility Entrance v1 — Phase 1

Scope: physical entrances only. Worker Select / replacement avatars / Load Select / new near-miss logic are not implemented in this phase.

## Use

Select F10, F11, F12 or F19 → EXPLORE FACILITY → FIRST PERSON or THIRD PERSON. Walk through the teal personnel doorway. F10/F11/F12 use the first factory bay; F19 uses the east side of the active PublicDataVisual because the adjacent building obstructs its south frontage. F01 has a marked dock walking access at the open south end of the drydock. It uses low steps and a landing, not a building door or portal.

Personnel doors start open. F opens/closes the leaf when looking at it. Walking through performs entry/exit; F does not teleport. E retains POI details. If F and E are pressed together while aiming at a personnel doorway, only the F door action runs. Closing is refused while the player straddles the threshold. The HUD has one interaction prompt; the nearest visible aimed entrance wins only when no closer POI/solid obstruction is in front of it.

The HUD shows INTERIOR / EXTERIOR for the current facility entrance. Leaving the building does not end Explore, release the crane input guard or change the camera mode. ESC / the existing Back to Facility action still ends Explore.

## Cause and fix

The previous F10/F11/F12 facade and roller shutters were closed box meshes reused as nonconvex MeshColliders. They had no door aperture or hollow interior. F19's active PublicDataVisual used a full-volume Factory collision BoxCollider. Exterior capsule casts at the former shutter position hit these blockers. There was no one-shot entrance/portal state to reset; a closed visual/collision shell was the underlying problem.

Only the first factory bay is hollowed for F10/F11/F12. The original batched mesh assets are preserved; new copies remove that bay's solid box and keep the other bays/decorations. Separate visible shell sections and solid wall colliders leave a clear 1.2×2.2 UU personnel opening. The neighbouring shutter keeps its 3.256 UU height and is shifted slightly to leave room for the personnel opening. F19 keeps its active 21×5.4 UU footprint, estimated 4 UU wall height and 2.6 UU front shutter. Its disabled legacy model/colliders stay disabled. Its full-volume active collision is replaced by a hollow shell with an east-side opening. Small interior/approach floors keep the threshold traversable.

FacilityEntrance observes Scene geometry containment every frame, rather than consuming a one-shot trigger. It counts repeat entries and exits. The InteriorZone is an explicit trigger-shaped geometric volume; containment uses InverseTransformPoint and box extents directly, independent of trigger-event delivery. Door leaf/collider updates apply only to the newly created doorway. No crane or worker-route commands are issued.

## Scale domains

Operational simulation 1:1 / Shipyard background 1:10 reconstruction (DEMO)

작업 시뮬레이션 1:1 / 조선소 배경 1:10 축소 배치 (DEMO)

Personnel clearance and Explorer movement use operational UU (1 UU = 1m). Collider overlap/containment uses the existing Scene world geometry directly. Shipyard background conversion remains solely ShipyardCoordinateConfig.metersPerUnityUnit. No new 10/0.1 conversion constant is introduced. The entrance floor thicknesses, clearances and walking sample distances are geometry values, not conversion coefficients. This does not resolve the whole prototype's dual-scale layout or turn the demonstration into a field access permit.

## Protected systems

Voice, Operator, Actual Motion, Verification, Prediction, Swept Area, LoadProfile, LiveLoadPath, Worker Hazard, WorkerRoute, Accident Search, PLAN and STOP logic are unchanged. No Humanoid/model download. No worker/load selection assets or placeholder scripts. LIVE TowerCraneRoot remains unparented at (-46,0,32), identity rotation, unit scale. Existing DB labels and value-status policies remain unchanged.

## Validation

FacilityEntranceChecks uses the normal Explore spawn and CharacterController.Move for every approach/crossing, with test-only obstacle-aware approach planning. It never writes the player's Transform or calls Teleport to cross a doorway. Each facility performs three entries and three exits, ending outside, including lateral offsets at the doorway edges. Virtual W/E/F input verifies movement and input separation. Existing suites and their expected values remain unchanged. FacilityEntranceRegressionRunner orchestrates fresh Play sessions, isolates physical mouse input during existing Shipyard pointer tests, and temporarily displays the legacy Operator UI for its existing tests. Fixtures are restored or discarded on leaving Play.

Additional regression found missing Korean Command header glyphs in the existing fallback atlas. OperatorCommand Korean SDF.asset was populated from the existing MalgunGothic font for the existing localization strings; no external font or localization logic change.