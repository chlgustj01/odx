# Phase 1 BLOCKER 복구 보고서

대상: `D:\UnityProject\CraneSignalAI`, `Assets/Scenes/Main.unity`. 작성일: 2026-10-21 KST. **최종 판정: CAUTION**. Worker Select / Avatar / WorkerProfile / Load Select / 새 LoadProfile / NearMiss / Phase 2·3 stub은 만들지 않았다. Entrance 재설계, LIVE root 변경, 폰트·MCP package 수정, Scene 저장, Git push도 하지 않았다.

## 1. 승인 요약

기존 runner의 Worker rebasing은 제거했다. legacy test 및 expected는 그대로 유지했고 원래 world fixture의 실패를 숨기지 않았다. 원점 기반 Scene 백업·당시 동일한 테스트 소스·당시 PASS 결과·이동 기록이 서로 일치하여 **LEGACY / OBSOLETE GEOMETRY ASSUMPTION**으로 판정한다. 신규 scene-relative 검증은 별도 파일로 수행하며 기존 legacy suite를 대체/삭제하지 않는다.

| 항목 | 판정 | 근거 |
|---|---|---|
| Worker 강제 이동 제거 | PASS | runner에 Worker Transform 쓰기 없음; legacy trace는 원래 world fixture 유지 |
| 기존 test / expected | PASS | 기존 12개 해시 동일; legacy 원본 및 작성 당시 보관본도 동일 |
| legacy LiveLoad | LEGACY / OBSOLETE GEOMETRY ASSUMPTION | E-Worker assertion FAIL 그대로 보존. 8개 선행 결과 PASS, 실패 이후 나머지 항목 미실행 |
| 신규 scene-relative | PASS | 동일 translation, position/angle tolerance, 실제 Runtime hazard 검증 |
| Negative control | PASS | swept 경계 거리 37.703228 UU, SAFE / WorkerInRemainingArea=false |
| 제품 Runtime / Entrance | PASS | 103 / Confidence / Entrance / Timeline / Operator / HMI 결과 아래 참조 |
| 최종 | CAUTION | legacy 실패는 obsolete로 분리하며 동일 조건 220 PASS라고 주장하지 않음 |

## 2. 작업 전 백업

`D:\UnityProject\CraneSignalAI\BlockerFix\Phase1_PostAudit_Blocker_Baseline`에 `baseline.bundle`, `working-tree-overlay.zip`, `Baseline.json`, `GitStatus.txt`를 보존했다. HEAD `5970d20427baf7106e3cda86b660c7da73e3252f`, baseline SHA manifest 1320개, overlay 361개. 기존 미커밋/미추적 변경을 보존했으며 이를 이번 수정으로 오인하지 않는다. Git push/commit은 하지 않았다.

- baseline.bundle SHA-256: `1cd2f54869461746cd0f89137016c089dcdb701248498db0b1a9ac4171b3887f`

- working-tree-overlay.zip SHA-256: `cfcab9fccbff0a352c935c5cf8255b3abaf48b0d7399dd20edf61f421be58b0b`

- Baseline.json SHA-256: `68a3c48afe4508b084ab6a167919f166bd699cb47058171d6181baaf8a54d0fe`

## 3. 변경 파일 / diff

프로젝트 실행 코드 변경은 기존 runner 1개와 신규 Editor test 1개뿐이다. 신규 test `.meta`는 Unity가 생성했다. 보고서/BlockerFix 증거는 실행 코드와 별도다. [ChangedFiles.json](../BlockerFix/ChangedFiles.json).

| 파일 | 변경 전 SHA-256 | 변경 후 SHA-256 |
|---|---|---|
| Assets/Editor/UI/FacilityEntranceRegressionRunner.cs | `c4b227a331b45b794417ec77bfd1027d3c520c054d025591c02134710716a631` | `0f53c48dd63b578b734c02eecfe7c45f4bb9c38df8c1b41a9f270e2f15c625dd` |
| Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs | 없음 (N/A) | `d28023582f00fe4da3de0485f6ee710b0f53beab67bfeb77a567b8c7bcd9a348` |
| Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs.meta | 없음 (N/A) | `7a5859629508c3e193a4dd19b4e23a00080a7acff07171af988364ffbed677db` |

runner 수정 전/후 전체 unified diff: [FacilityEntranceRegressionRunner.diff](../BlockerFix/FacilityEntranceRegressionRunner.diff). 원본/수정본도 `.before.cs.txt`, `.after.cs.txt`로 보존했다. 핵심 삭제:

```diff
- if (index == 5 && stage >= 1 && stage <= 6) GameObject.Find("Worker_01").transform.position = GameObject.Find("TowerCraneRoot").transform.position + new Vector3(Mathf.Sin(20 * Mathf.Deg2Rad) * 3, 0, Mathf.Cos(20 * Mathf.Deg2Rad) * 3);
```

원래 이유는 “legacy가 relocation 이전 원점을 전제로 한다”는 판단 아래 보호 대상 LIVE root 대신 Worker를 옮기려는 것이었다. 영향 범위는 LiveLoad `index=5` E-Worker의 `stage=1..6`. 이는 실제 Play Runtime Worker Transform을 바꾸어 full/remaining overlap 조건을 바꾸는 **B: 제품 실행 조건 변경**이다. 단순 자동화 보조가 아니다. 삭제 후 E-Worker의 `Worker/full hazard/remaining debug disagree` assertion이 실패했다. 정상 Scene의 일반 작업자 배치 문제와 테스트가 매 case에 강제로 쓰는 fixture를 구분했다.

## 4. harness 보조별 ablation / 처리

| 보조 | 분류 | 제거 상태 실행 결과 | 최종 처리 / 이유 |
|---|---|---|---|
| Worker_01 root rebase | B | 원래 world fixture에서 E-Worker FAIL | 삭제. legacy 실행에서 Worker/geometry/hazard state 쓰기 없음 |
| held keyboard 재공급 | A 후보 | raw clock: 유무 모두 B-TROLLEY sampling FAIL. 60Hz: 유무 모두 E-Worker geometry FAIL | 삭제. 필요성 입증 안 됨; 기존 suite가 자기 input을 소유 |
| Mouse 임시 비활성화 | A 후보 | Shipyard 19 PASS (격리 없음) | 삭제. 필요성 없음 |
| CommandPage/ancestor 강제 활성화 | 강제 SetActive는 B, 테스트 UI 진입 목적은 A | UI 진입 자체 생략 시 legacy Run에서 inactive panel lookup 실패 | 강제 ancestor 활성화 삭제. 공개 SelectCrane + SelectCommand로 정상 UI 진입만 유지 |
| Canvas 강제 토글 | B (UI raycast 환경 우회) | 토글 없이 Operator UI 18 PASS | 삭제 |
| overview/binder 비활성화 | B (제품 UI lifecycle 우회) | 비활성화 없이 Operator UI 18 PASS | 삭제 |

증거: `Probes/A_NoHeldInput`, `A_WithHeldInput`, `B_NoMouseIsolation`, `C_NoCommandPage/HarnessError.txt`, `D_NoCanvasToggle`, `E_NoOverviewDisable`; `TimingControls/NoHeld_60Hz`, `Held_60Hz`. C의 NullReference는 legacy test가 inactive `10 Operator Command`를 GameObject.Find로 찾는 사전조건 실패로 파일에 기록했으며, Runtime 기능 regression 또는 Console Error로 위장하지 않았다. UI를 정상 경로로 연 두 실행은 모두 PASS했다.

### 별도 60Hz timing control

raw-clock LiveLoad의 최초 실패는 geometry가 아니라 B-TROLLEY `Committed a pose every frame`이었다. Runtime은 `actualDistanceStepM=0.075`이고 TROLLEY 속도는 2 UU/s다. 프레임 간 이동이 0.075 이상이면 실제 경로를 매 프레임 commit하는 것이 정상이며, legacy의 `ActualSampleCount < elapsedFrames`는 충분한 simulation FPS를 가정한다. 경계는 약 26.67 FPS다. raw-clock 실패는 보존했다.

이를 분리하기 위해 **명시적으로 요청한 legacy 진단 job에만** `Time.captureDeltaTime=1/60`의 opt-in 테스트 시계를 사용했다. 입력 이벤트·Worker·Scene·controller/sampler/expected는 바꾸지 않았다. 해당 조건에서 TROLLEY/HOIST까지 통과하고 E-Worker에서 SAFE/remaining=false로 실패했다. 유/무 held 대조가 같은 기하 실패를 보이므로 held 재공급을 유지하지 않았다. 이 시계는 **실제 wall-clock baseline과 동일하다는 주장 대상이 아니며**, 동일 조건 220 PASS라고 부르지 않는다. 테스트 환경의 프레임 가정을 고정한 별도 진단/legacy 실행이다. 최종 runner의 기본 Start 경로는 clock=false이며 원래 시계를 유지한다. 순차 최종 실행의 04 job은 명시적 60Hz 진단 조건이었다. 그 후 기본 경로와 동일한 no-clock/no-helper legacy도 RestoredLegacyRaw 및 PostFixValidation에서 다시 실행해 sampling 실패를 별도 보존했다. 순차 실행 이후 변경은 default clock 제거와 아래의 요청 파일 bridge 제거뿐이다. `FormalOrderedHarness.source.txt` 및 [FormalToFinalHarness.diff](../BlockerFix/FormalToFinalHarness.diff)로 구분한다. 완료/실패 시 이전 captureDeltaTime을 복구했고 최종값은 0.0이다. Runtime scale constant/asset은 수정하지 않았다.

유지된 A 보조(주석 명시): GameView focus/repaint/PlayerLoop scheduling, 공개 crane/COMMAND navigation, opt-in legacy 진단 job의 timing control. MCP domain reload 동안 테스트 구동을 이어가려 임시 추가했던 JSON 요청 bridge는 공유 충돌을 발견해 **최종 코드에서 전체 제거**했다. 최종 runner는 inactive 상태에서 파일을 읽지 않고 즉시 return하며, 명시적인 Start/StartJobs API로만 실행한다. package/connection logic은 바꾸지 않았다.

## 5. legacy 설계 좌표계 / obsolete 증거

판정 기준은 **legacy 작성 당시 설계 좌표계 R_old=(0,0,0)**다. 이를 선택한 이유는 추측이 아니라 다음 독립된 기록들이 일치하기 때문이다.

1. `History/live-load_Main.before.unity.txt`의 TowerCraneRoot 실제 Transform: position(0,0,0), identity quaternion, unit scale, parent null.
2. 작성 당시 `work/live-load/LiveLoadPathPlayModeChecks.cs`와 현재 원본 SHA-256이 동일: `1f24eec45461e98178ee61c7eaf4a090d2df3577afcda9bdb7fe343757ca8283`. 증거 사본 `History/Original_LiveLoadPathPlayModeChecks.cs.txt`.
3. 당시 `History/LiveLoad_final-state.json`: load=(0,6.05,3); `History/LiveLoad_tests.json`: 전체14 PASS, E-Worker mid-motion load=(0.6462,6.05,2.9296), WorkerInRemainingArea=true, DANGER.
4. `History/Main.before-tower-relocation.unity.txt`: 이동 직전에도 root=(0,0,0). `History/TowerRelocation.json`: old(0,0,0)→new(-46,0,32), local pivots unchanged. 후속 relocation checks의 predicted path 시작점=(-46,6.05,35).
5. Phase 1 직전 Backup의 root Transform은 이미 (-46,0,32): [Phase1PreSceneRoot.json](../BlockerFix/Phase1PreSceneRoot.json).
6. Runtime `PredictedPathEngine`은 SlewPivot.position/world center에 quaternion rotation을 적용하고, `HazardZoneEvaluator`는 worker.FootPosition과 swept polygon의 world XZ를 비교한다. 원점/로컬 좌표를 자동 보정하지 않는다. 이 Runtime 소스 해시는 불변이다.

위 증거의 원문 Scene/결과/해시/파일 timestamp는 `History/` 및 [HistoryProvenance.json](../BlockerFix/HistoryProvenance.json)에 보존했다. 위 relocation 직전 Scene 사본의 정확한 파일명은 `History/work_Main.before-tower-relocation.unity.txt`다. **Git에는 초기 import commit 1개만 있어 relocation 자체의 독립 commit/정확한 실행시각은 증거 없음.** 이동 기록 파일의 filesystem mtime은 2026-10-19 20:48:04 KST, 직전 Scene 백업 mtime은 20:09:53이며 이는 보조 provenance이지 인증된 이벤트 timestamp가 아니다. 초기 import(2026-10-20 14:34:12 KST)에는 이미 이동된 Scene이 들어 있다. 정확한 이동 시각을 Git으로 확정하지 않는다.

따라서 Phase 1 직전의 relocated LIVE crane 배치는 의도된 정상 Scene 배치였지만, legacy test의 원점 고정 Worker fixture와는 이미 불일치했다. Phase 1 Entrance 때문에 생성된 Runtime regression이라는 증거는 없다. **정상 제품 Scene과 obsolete 테스트 geometry를 구분한다.**

legacy 원본은 E-Worker에서 아래 world 위치를 그대로 쓴다:

```csharp
new Vector3(Mathf.Sin(20*Mathf.Deg2Rad)*3,0,Mathf.Cos(20*Mathf.Deg2Rad)*3)
```

| 비교 | legacy 설계(origin) | 현재 Scene에서 legacy 그대로 | 신규 scene-relative |
|---|---:|---:|---:|
| root world | (0,0,0) | (-46,0,32) | (-46,0,32) |
| Worker world | (1.026060,0,2.819078) | (1.026060,0,2.819078) | (-44.973938,0,34.819077) |
| Worker−root 거리 | 3 UU | 55.344165 UU | 3 UU |
| 20° sweep 중심 XZ 거리 | 0 | 56.035703 UU | 약 0 |
| full/remaining 초기 overlap | true/true | false/false | true/true |

최종 legacy geometry trace: `Final/04_LegacyLiveLoad/GeometryTrace.jsonl`. `index=5`에서 Worker world 좌표가 원본 그대로이며 root(-46,0,32), 실제 load는 root 근처, hazard=SAFE / WorkerInRemainingArea=false다. fixture 이동으로 DANGER를 만들어 PASS시키지 않았다.

## 6. 기존 test / expected 보존

[LegacyTestHashes.json](../BlockerFix/LegacyTestHashes.json)에 Phase 1 전 / 이번 복구 전 / 현재 세 SHA-256을 기록했다. 기존 test/runner 12개는 모두 그대로다. 이번에 변경한 FacilityEntranceRegressionRunner는 Phase 1에서 새로 추가됐던 runner로 구분한다. legacy expected 숫자/문구/assertion 수정 0개. 원본 파일 전체 사본 및 runner diff를 함께 제공한다.

| 기존 파일 | Phase 1 전 / 이번 작업 전 / 후 SHA-256 (모두 같음) |
|---|---|
| Assets/Editor/AccidentEvidence/AccidentEvidenceChecks.cs | `2adcab8ebfa67791db6cd8f3d9f4fa41511d44951c045f97bc9af21d3e585192` |
| Assets/Editor/Prediction/LiveLoadPathPlayModeChecks.cs | `1f24eec45461e98178ee61c7eaf4a090d2df3577afcda9bdb7fe343757ca8283` |
| Assets/Editor/ShipyardDB/ConfidenceRegressionRunner.cs | `8d1a17ddd7446ff85c0f8780bf2a75880bb60137a7259c3b7864f3059a226f02` |
| Assets/Editor/ShipyardDB/FacilityValueEvidenceChecks.cs | `6b5c951ea94ac3d80059fbdc7228b8b11bac162e772469374483aead602eea89` |
| Assets/Editor/ShipyardDB/ShipyardConfidenceChecks.cs | `4d241581c4eeb6f8c07cd32d4e3dc731f11982fffc0d7dd16b2cd2bc0e9b5322` |
| Assets/Editor/ShipyardDB/ShipyardPublicDataChecks.cs | `d4c28c3cb0f9c5ff6ffc93bec3474fda6e1ffa58cee77b6c296f0d2580f82d72` |
| Assets/Editor/Timing/CommandTimelinePlayModeChecks.cs | `19219866f6936180663a521e149a3be21d87349364437bec9c0ada63790b2f62` |
| Assets/Editor/UI/OperatorCommandUIPlayModeChecks.cs | `5743d1958e52b434baa9e72a0a2cfd0108e51677966f5b5b93c454ad882d2fee` |
| Assets/Editor/UI/ShipyardExploreChecks.cs | `fdef9fd7d7b1dadc4437dbd6a297054bb88aa3fd0cfdb194e903faed3ebb696a` |
| Assets/Editor/UI/ShipyardPlayChecks.cs | `170adfde3ff5b2f6e598e629cdc947da8453901d1b4798f832de0213cb0b30f4` |
| Assets/Editor/UI/TaskHmiPlayChecks.cs | `00481633ffd1ecd2f7fc222063610a669f60cfe1c5ea5761bb932fdb012ba613` |
| Assets/Editor/UI/WorkerRouteRiskChecks.cs | `b3e4ac9d33a4eacf1d0e81a8be046151736442832dd341213e8c3ac90d88b643` |

## 7. 신규 LiveLoadSceneRelativeChecks

파일: `D:\UnityProject\CraneSignalAI\Assets\Editor\Prediction\LiveLoadSceneRelativeChecks.cs`. 기존 legacy를 복제/패치하여 몰래 통과시키는 경로가 아니라 별도 test entry다. 역사적 R_old=(0,0,0), R_new=(-46,0,32), T=(-46,0,32)를 사용한다. LIVE root는 전혀 이동하지 않고, 실제 Runtime이 생성한 full/remaining path와 hazard를 읽는다. 새 test 안에서만 Worker fixture와 원래 경로 sample/footprint reference 모두에 같은 T를 적용한다. LoadProfile·crane pivot은 test setup에서 조정하지 않으며 historical precondition이 다르면 실패한다. 운동은 실제 InputSystem D key→기존 TowerCraneController를 통해서만 발생한다.

16개 0..30° 경로 point와 인접 15개 swept segment corner를 비교한다. 원래 full hazard / remaining debug / 완료 후 full DANGER 유지·remaining false 의미를 동일하게 검증한다. Voice-only 동안 crane pose 불변과 STOP clear도 확인한다.

| 검증 | 측정 결과 | 허용값 |
|---|---:|---:|
| Worker−sweep 상대 위치 오차 | 2.19811068e-06 | .01 UU |
| 상대 거리 오차 | 4.76837158e-07 | .01 UU |
| 경로 최대 오차 | 3.81469727e-06 | .01 UU |
| swept corner 최대 오차 | 5.39479652e-06 | .01 UU |
| 경로 bearing 최대 오차 | 4.386902e-05 | .1 degree |
| Worker−sweep 상대 bearing 오차(별도 cross-check) | 6.164638376e-05 | .1 degree |

oldRelative=[1.02606034, -6.05, -0.180922031], newRelative=[1.026062, -6.05, -0.180923462]; 거리 6.139058→6.13905859 UU. oldInside/newInside=true/true. geometry/angle tolerance 모두 PASS. 결과: [Final/05_SceneRelative/SceneRelative_tests.json](../BlockerFix/Final/05_SceneRelative/SceneRelative_tests.json), [RigidTransformCrossCheck.json](../BlockerFix/RigidTransformCrossCheck.json). 상대 bearing cross-check는 test가 기록한 실제 old/new vector로 독립 계산한 추가 검증이다.

Negative control: test-only `NEGATIVE_CONTROL_DISTANCE=30.0`을 operational 1 UU=1m로 적용했다. sweep 중심에서만 멀다고 가정하지 않고 **모든 swept polygon edge까지 최소 거리**를 계산했다. 측정 거리 37.703228 UU, WorkerInRemainingArea=false, 실제 HazardState=SAFE. Worker를 같은 positive fixture로 복귀시킨 뒤 DANGER 재진입도 확인했다. 항상 DANGER/항상 PASS하는 test가 아님을 보였다. 이 상수는 Editor test 내부 진단뿐이며 Scene/prefab/Runtime data/production code에 저장하지 않았다.

## 8. 요청 순서의 최종 재테스트

`Final/RunnerStatus.json`과 각 suite JSON이 원문 증거다. ablation/probe와 최종 회귀를 구분했다. 기존 test 자체의 실패를 expected 수정으로 감추지 않았다.

| 순서 | suite | 결과 |
|---|---|---|
| 1 | 기존 regression 103 | PASS (103개) |
| 2 | Confidence 17 | PASS (17개) |
| 3 | Entrance Walking 44 | PASS (44개) |
| 4 | LiveLoad legacy (명시적 60Hz 진단) | LEGACY / OBSOLETE GEOMETRY ASSUMPTION — E-Worker FAIL; 선행 8 결과 PASS |
| 5 | 신규 scene-relative | PASS (8개) |
| 6 | Timeline | PASS (11개) |
| 7 | Operator UI | PASS (18개) |
| 8 | HMI | PASS (13개) |

103 세부 결과:

- `Final/01_Original103/AccidentEvidence/Core_tests.json`: 12 PASS

- `Final/01_Original103/AccidentEvidence/Play_tests.json`: 13 PASS

- `Final/01_Original103/DB/DB_Play_tests.json`: 16 PASS

- `Final/01_Original103/Edit/Structure_tests.json`: 15 PASS

- `Final/01_Original103/Explore/Explore_tests.json`: 17 PASS

- `Final/01_Original103/Regression/Shipyard_tests.json`: 19 PASS

- `Final/01_Original103/WorkerRoute/WorkerRoute_tests.json`: 11 PASS

Entrance: 기존 `FacilityEntranceChecks.cs` unchanged, 44개 결과 및 WalkingTrajectory.json으로 F10/F11/F12/F19 재진입, F01 Dock access와 A~Y 관련 검증을 보존했다. 기존 CharacterController.Move 기반 접근·문턱·재진입을 사용하며 player teleport로 통과시키지 않았다. LIVE rig 보존도 해당 suite가 검사한다. Entrance Runtime/Editor setup/mesh/material의 hash는 변경되지 않았다.

legacy suite는 E-Worker 직전까지 8개 결과를 기록한 뒤 assertion에서 중단했으며 이후 5개 항목은 실행되지 않았다. 따라서 “legacy14 PASS” 또는 “220 동일 조건 PASS”라고 집계하지 않는다. 새로운 8개 scene-relative 결과는 별도 표기한다.

최종 코드의 원래 clock / 무보조 추가 재실행: [PostFixValidation/LegacyRaw/LiveLoad_tests.json](../BlockerFix/PostFixValidation/LegacyRaw/LiveLoad_tests.json) — FAIL, 선행 2개 결과 후 `System.Exception: B-TROLLEY: Committed a pose every frame`. 앞선 `RestoredLegacyRaw/Legacy_NoClock_NoHelpers/GeometryTrace.jsonl`에는 captureDeltaTime=0, deltaTime 약 0.04165초인 실제 프레임이 기록되어 있다(2UU/s에서 약 0.0833UU/frame > 0.075UU sampling threshold). 이는 raw-clock decimation assertion의 FPS 전제를 드러낸다. 제품 sampler의 정상 거리 기반 commit을 오류로 고쳐서 통과시키지 않았다. 60Hz geometry 진단 결과와 구분한다.

## 9. Console / compiler / 보호 상태

Console snapshot [FinalCurrent/Console.json](../BlockerFix/FinalCurrent/Console.json)와 분류 [FinalCurrent_ConsoleClassification.json](../BlockerFix/FinalCurrent_ConsoleClassification.json). **전체 Error 14 / Warning 159**. 경고를 숨기거나 0으로 축약하지 않는다. 기존 Console 자동 ClearOnPlay/ClearOnRecompile만 로그 보존 목적으로 임시 해제했으며 마지막에 원래 flags를 복구했다. Console Clear/level filter/package patch는 하지 않았다.

| 원인 | Error | Warning |
|---|---:|---:|
| C# compiler | 0 | 0 |
| project Runtime | 0 | 0 |
| project Editor | 1 | 0 |
| MCP package | 13 | 159 |
| Unity native/system | 0 | 0 |

MCP 항목은 disposed Flush / auto-reconnect disabled / connection timeout·disconnect·stop/gate lifecycle stack을 구분해 원문 보존했다. Assets/Scripts 또는 Assets/Editor source frame이 없는 package/connection 오류를 프로젝트 Runtime 오류로 분류하지 않았다. 테스트 assertion failure는 suite JSON에 남기며 Console Error와 혼동하지 않는다. C# compile 진단은 final Console 및 이번 작업 Editor.log compile 구간에 없었다.

**프로젝트 Editor Error 이력 1건을 숨기지 않는다.** 이번에 임시 추가한 요청 bridge가 writer와 동시에 `RunnerRequest.json`을 읽어 `IOException: Sharing violation`을 발생시켰다. stack은 FacilityEntranceRegressionRunner.TryRequest→Tick이며 MCP 오류가 아니다. 이는 이번 작업 중 생긴 실제 harness 결함이었다. 최종 코드에서 TryRequest/Request/polling 경로를 모두 제거했고 [FormalToFinalHarness.diff](../BlockerFix/FormalToFinalHarness.diff)에 남겼다. 기존 Console Error는 Clear하지 않아 위 전체/Editor 개수에 여전히 포함된다. [FileBridgeRemovalProof.json](../BlockerFix/FileBridgeRemovalProof.json)의 독점 파일 잠금 상태 10 Editor update 재검증: polling 메서드 없음, Error 14→14, 새 Error 0, Scene dirty=false. 이후 PostFixValidation에서 no-clock legacy의 기존 sampling 실패와 scene-relative 8 PASS/negative control PASS를 재확인했다. **미해결 project Runtime/Editor 및 compiler warning/error는 0건**으로 판정하며, 역사적 Editor Error 1건과 분리한다.

Font SDF: **범위 밖 기존 변경 유지 / 이번 작업 수정 없음**. 문자셋 추가 조사, glyph/atlas/fallback/dynamic 설정 변경을 하지 않았다. 세 SDF asset hash가 이번 Backup과 동일함만 확인했다.

[ProtectedHashes.json](../BlockerFix/ProtectedHashes.json)의 175개 보호 파일 모두 unchanged=True. Main Scene은 저장된 기존 파일을 유지했고 **이번 작업 Scene 저장 0회 / dirty=False / missing script=0 / Play Mode=False**. Scene SHA-256 `6462cb7d8199402ff495a4fefbbcdca8968452408a12f2362bcc9fcd2a80e929`는 Backup과 같다. Runtime C#·Entrance asset·Packages·폰트 파일 불변이다.

LIVE 최종: position [-46.0, 0.0, 32.0], quaternion [0.0, 0.0, 0.0, 1.0], scale [1.0, 1.0, 1.0], parent None. Controller.enabled=True, CommandReceiver.enabled=True. Fault: component.enabled=False, faultEnabled=False, faultMode=NONE. 기대값 일치. 상세 [FinalCurrent/State.json](../BlockerFix/FinalCurrent/State.json).

## 10. 최종 판정 / 중단

**CAUTION — Post-Audit의 “legacy runner가 Worker를 재배치한다” BLOCKER는 해소됐다.** 기존 test/expected를 보존한 채 obsolete geometry를 코드·역사 기록·실제 실패로 증명했고, 별도 scene-relative relative geometry와 negative control이 PASS했다. 제품 Runtime / Entrance regression은 발견되지 않았다. 다만 과거 220 PASS의 실행 동등성이 소급 복구되는 것은 아니다. raw-clock legacy의 sampling 전제 실패 및 명시적 60Hz 진단 조건도 계속 기록으로 남긴다.

**Phase 2 Worker Select 시작 가능 여부: CAUTION 조건으로 가능.** 이번 작업에서는 Phase 2를 시작하지 않는다. 보고 후 중단한다.
