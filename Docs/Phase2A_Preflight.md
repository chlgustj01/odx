# Phase 2A Preflight

2A only. 2B approval pending; no stage/exclusion/near-miss/session-risk rules.

## Coverage map

| Legacy item | Assertion meaning | Reachability before | Gate / gap | Action |
|---|---|---|---|---|
| A-SLEW-MidMotion | Voice stationary; immutable full/target; actual decimation; remaining endpoints/current footprint/rotation | raw PASS; 60Hz PASS | SceneRelative 8 partial; detailed sampling/endpoints gap | ADD G-SamplingEndpoints |
| A-SLEW-Completed | Remaining empty/zero; Full immutable | raw PASS; 60Hz PASS | SceneRelative completion + Operator A-completed | COVERED |
| B-TROLLEY-MidMotion | Radial current/remaining shape + actual decimation | raw FAIL before completion; later cases not reached | Operator B-moving reads values only; geometry gap | ADD G-TrolleyGeometry |
| B-TROLLEY-Completed | Numeric remaining zero; full snapshot unchanged | raw NOT RUN; 60Hz PASS | Operator B-completed + HMI actual/remaining partial | ADD G-TrolleyGeometry completion |
| C-HOIST-MidMotion | Vertical path; unchanged XZ footprint; current outline | raw NOT RUN; 60Hz PASS | Operator C moving value equality; XZ geometry gap | ADD G-HoistGeometry |
| C-HOIST-Completed | Remaining zero/empty + full preserved | raw NOT RUN; 60Hz PASS | Operator C completed | COVERED + appended completion |
| D-SmallLoad | Footprint area=2 and visual follows | raw NOT RUN; 60Hz PASS | SceneRelative fixed 1x2 corner check partial | ADD G-LoadDimensionScaling |
| D-LargeLoad | 4x2 area=8; 4x area; bounds growth >1.5; visual scale | raw NOT RUN; 60Hz PASS | No current Gate validates dimension-change response | ADD G-LoadDimensionScaling |
| E-Worker-MidMotion | Full DANGER and WorkerInRemainingArea simultaneously | raw NOT RUN; 60Hz FAIL (obsolete fixture) | SceneRelative positive + mid-motion and negative control | COVERED |
| E-Worker-Completed | Full DANGER remains after remaining empty + worker false | raw NOT RUN; 60Hz NOT RUN after E failure | SceneRelative completed + STOP | COVERED |
| Wrap-MidMotion | 350→380 unwrapped target + remaining endpoints/orientation | raw NOT RUN; 60Hz NOT RUN | No Gate specifically crosses 360 | ADD G-Wrap360 |
| Wrap-Completed | Unwrapped target reach + full preserved | raw NOT RUN; 60Hz NOT RUN | No Gate specifically crosses 360 | ADD G-Wrap360 completion |
| WrongDirection | Opposite operator increases remaining; original target fixed | raw NOT RUN; 60Hz NOT RUN | Timeline C tests mismatch, not increasing remaining | ADD G-WrongDirectionRemaining |
| STOP-History | Voice STOP clears full/remaining but retains footprint/history; operator Space STOP verified | raw NOT RUN; 60Hz NOT RUN | Timeline H/Operator J/HMI Extra-Stop cover STOP; history retention gap | ADD G-StopHistory |

Additions are append-only SceneRelative checks. Existing 8 rows/negative control and all legacy assertions/expected values remain unchanged. A test-only fixed 60Hz diagnostic clock is recorded for distance-decimation checks; the legacy informational run retains its original clock/input/fixture.

## Console baseline

{
  "errors": 19,
  "warnings": 160,
  "counts": {
    "MCP package": {
      "Error": 18,
      "Warning": 160,
      "Log": 0
    },
    "Project Runtime": {
      "Error": 0,
      "Warning": 0,
      "Log": 193
    },
    "Project Editor": {
      "Error": 1,
      "Warning": 0,
      "Log": 10
    }
  }
}

Editor restart cannot be established from Console: retained historical entries exist. They were preserved rather than cleared. All deltas use this actual captured baseline.

## FPS baseline

{
  "label": "Before",
  "seconds": 10.014096700004302,
  "frames": 129,
  "averageFps": 12.881840855395833,
  "minimumFps": 7.822039221170689,
  "averageFrameTimeMs": 77.62865658918064,
  "player": [
    -51.0,
    0.004999999,
    -35.0
  ],
  "camera": [
    -51.0,
    2.005,
    -39.0
  ],
  "cameraRotation": [
    0.0,
    0.0,
    0.0,
    1.0
  ],
  "yaw": 0.0,
  "pitch": 0.0,
  "facility": "F10",
  "mode": "THIRD_PERSON",
  "screen": [
    1920,
    1080
  ],
  "eyeHeight": 1.7,
  "quality": 0,
  "captureDeltaTime": 0.0,
  "playerDrift": 0.0,
  "cameraDrift": 0.0,
  "method": "Editor GameView real frame intervals; 2.5s warmup then >=10s; fixed validated F10 spawn, third person, cursor unlocked, no product features disabled"
}

## Route planner

WorkerRouteRiskEngine stores pre-authored profile.direct/profile.detour arrays. RoleWeight changes hazard scoring, not zone-cost path planning. There is no route solver input for role preference; Phase2A therefore adds preference data and candidate cost-bias evaluation only. No new pathfinding system.

## Accident mapping

{
  "field": {
    "column": "CM-H04",
    "displayName": "재해자 역할",
    "options": [
      {
        "code": "1",
        "label": "크레인 운전원",
        "unknown": false
      },
      {
        "code": "2",
        "label": "신호수",
        "unknown": false
      },
      {
        "code": "3",
        "label": "줄걸이·도비공",
        "unknown": false
      },
      {
        "code": "4",
        "label": "유도원",
        "unknown": false
      },
      {
        "code": "5",
        "label": "통행·무관 작업자",
        "unknown": false
      },
      {
        "code": "9",
        "label": "미상",
        "unknown": true
      }
    ]
  },
  "normalizedField": "workerRoleCode",
  "rows": [
    {
      "code": null,
      "label": null,
      "count": 661
    },
    {
      "code": "3",
      "label": "줄걸이·도비공",
      "count": 65
    },
    {
      "code": "2",
      "label": "신호수",
      "count": 35
    },
    {
      "code": "4",
      "label": "유도원",
      "count": 7
    },
    {
      "code": "1",
      "label": "크레인 운전원",
      "count": 23
    },
    {
      "code": "5",
      "label": "통행·무관 작업자",
      "count": 11
    }
  ],
  "mapSignaler": "2",
  "mapRigger": "3",
  "welder": "NOT_MAPPED",
  "outfit": "NOT_MAPPED"
}