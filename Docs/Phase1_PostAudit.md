# Phase 1 Post-Audit — 확인·보고 전용

감사일: 2026-10-21 (KST). 대상: `D:\UnityProject\CraneSignalAI`, `Assets/Scenes/Main.unity`. Phase 2를 시작하지 않았다. 이번 감사에서는 보고서/증거만 저장했으며 Scene, Entrance, Runtime, test/runner, package 소스를 수정하지 않았다. Worker Select / Avatar / Load 코드·데이터·prefab·stub을 만들지 않았다.

**승인 권고: 아니오. 새 runner의 LiveLoad Worker fixture 이동이 실제 Phase 1 이전 배치 대비 상대 거리와 위험 판정 조건을 바꾸므로 요청된 판정 기준상 BLOCKER다. 기존 test 파일 자체와 expected value는 변경되지 않았다.**

| 항목 | 판정 | 근거 파일 / 요약 |
|---|---|---|
| 1. MCP gate timeout | PASS | OriginalGateWarnings.txt / OriginalGateDomainReloadContext.log / ReproductionResult.json. B: package lifecycle. 프로젝트 프레임 없음. 연결된 Play 진입·종료 1회에서 미재현(transient). |
| 2. test/runner 변경 | PASS | TestRunnerHashes.json. 기존 12개 변경 0개, 새 2개 별도 기록. expected 변경 0개. 새 harness의 실행 조건 변경은 2-1 참조. |
| 2-1. LiveLoad fixture 이동 | BLOCKER | LiveLoadFixture.diff / LiveLoadFixtureGeometry.json. LIVE root 상대 거리 55.344→3.000 UU; 동일 20° path 중심 XZ 거리 56.036→0 UU. 원래 기준과 동등하지 않음. |
| 3. SDF.asset 변경 | CAUTION | FontDiff.json / AllFontGlyphDiffs.json / OtherFonts.json. 범위 밖 폰트 3개: Korean +68, Dashboard +2, OperatorCommand +29. 기존 glyph/비글리프 설정 유지. |
| 4. Scene / Entrance | PASS | AfterReproductionState.json / EntranceSourceHashes.json / AuditNoMutation.json. dirty=false, Play 종료, missing=0. 감사 중 606개 입력 파일 불변. Phase 1 종료 Scene/새 asset의 독립 SHA manifest 부재 한계는 아래 명시. |
| 4. LIVE TowerCraneRoot | PASS | AfterReproductionState.json. (-46,0,32) / (0,0,0,1) / (1,1,1) / null. Controller/Receiver enabled=true. |
| 4. Fault | PASS | AfterReproductionState.json. component.enabled=false / faultEnabled=false / faultMode=NONE. |

## 1. MCP gate timeout 2건

2026-10-21 KST. 아래 원문과 전체 stack trace는 [OriginalGateWarnings.txt](../PostAudit/OriginalGateWarnings.txt)에 보존했다. raw Editor.log의 도메인 재로드 문맥은 [OriginalGateDomainReloadContext.log](../PostAudit/OriginalGateDomainReloadContext.log) (원본 줄 29710~29901 중심)이다.

```text
10:57:13:1841
ConnectionManager[f6d23702-f7e4-4782-8811-22c2e512567c] DisconnectImmediate Could not acquire gate within timeout. Proceeding without gate protection.
10:57:18:2024
ConnectionManager[f6d23702-f7e4-4782-8811-22c2e512567c] Dispose Could not acquire gate within timeout during Dispose. Proceeding with cleanup anyway.
```

두 stack 모두 `UnityEngine.Debug.LogWarning → UnityLogger.cs:125 → Microsoft.Extensions.Logging → McpPlugin.ConnectionManager.DisconnectImmediate / Dispose → BaseHubConnector → McpPlugin.Dispose → McpPlugin.Finalize`를 포함한다. 경로는 `Library/PackageCache/com.ivanmurzak.unity.mcp@d1e406c3eea4/...`이며 **Assets/Scripts / Assets/Editor 프레임은 두 건 모두 없다. A가 아닌 B(package lifecycle/connection teardown)로 분류한다.**

원본 로그의 `Begin MonoManager ReloadAssembly`와 `Mono: successfully reloaded assembly` 사이에 발생했다. Phase 1 OperatorUI→HMI fresh Play 전환과 시간상 연관되며 도메인 재로드/이전 plugin finalization은 확인된다. 당시 Enter/Exit/명시적 연결 해제 이벤트가 각각 timestamp로 남아 있지 않아 정확히 어느 버튼 동작이 직접 원인이었는지는 단정하지 않는다.

재현: MCP 연결 상태에서 domain reload를 사용하는 정상 Play 진입→종료 1회. domain reload 1회, gate timeout 0회. **transient / 이번 1회에서는 미재현**이며 해결됐다는 뜻은 아니다. package 수정·warning 숨김·level filtering·Console Clear는 하지 않았다. 로그 보존을 위해 Console 자동 ClearOnPlay/ClearOnRecompile 플래그만 잠시 해제하고 원값 8066으로 복구했다. [ReproductionAttempt.json](../PostAudit/ReproductionAttempt.json), [ReproductionEditor.log](../PostAudit/ReproductionEditor.log), [ReproductionResult.json](../PostAudit/ReproductionResult.json).

## 2. test/runner 전체 해시

비교 기준: C:\Users\chlgu\Documents\Codex\2026-10-19\unity-mcp-main-scene-hierarchy-towercraneroot\outputs\FacilityEntrances\Backup\Baseline.json 및 같은 Backup의 working-tree-overlay.zip. Git 기준 HEAD는 `5970d20427baf7106e3cda86b660c7da73e3252f`; 기존 미커밋 변경이 있으므로 HEAD만을 Phase 1 기준으로 오인하지 않았다. 기준 manifest 사본: [Phase1_Baseline.json](../PostAudit/Phase1_Baseline.json).

**기존 test/runner 변경 0개.** 103 regression 관련 파일을 포함하여 Backup에 있던 Checks/Runner C# 총 12개를 비교했다. LiveLoad/Timeline/Operator/HMI 등 103개 외 legacy suite도 함께 포함한다. 모든 before/after는 SHA-256 전체 값이다.

| 기존 파일 (프로젝트 상대 경로) | 변경 전 SHA-256 | 변경 후 SHA-256 |
|---|---|---|
| Assets/Editor/AccidentEvidence/AccidentEvidenceChecks.cs | `2adcab8ebfa67791db6cd8f3d9f4fa41511d44951c045f97bc9af21d3e585192` | `2adcab8ebfa67791db6cd8f3d9f4fa41511d44951c045f97bc9af21d3e585192` |
| Assets/Editor/Prediction/LiveLoadPathPlayModeChecks.cs | `1f24eec45461e98178ee61c7eaf4a090d2df3577afcda9bdb7fe343757ca8283` | `1f24eec45461e98178ee61c7eaf4a090d2df3577afcda9bdb7fe343757ca8283` |
| Assets/Editor/ShipyardDB/ConfidenceRegressionRunner.cs | `8d1a17ddd7446ff85c0f8780bf2a75880bb60137a7259c3b7864f3059a226f02` | `8d1a17ddd7446ff85c0f8780bf2a75880bb60137a7259c3b7864f3059a226f02` |
| Assets/Editor/ShipyardDB/FacilityValueEvidenceChecks.cs | `6b5c951ea94ac3d80059fbdc7228b8b11bac162e772469374483aead602eea89` | `6b5c951ea94ac3d80059fbdc7228b8b11bac162e772469374483aead602eea89` |
| Assets/Editor/ShipyardDB/ShipyardConfidenceChecks.cs | `4d241581c4eeb6f8c07cd32d4e3dc731f11982fffc0d7dd16b2cd2bc0e9b5322` | `4d241581c4eeb6f8c07cd32d4e3dc731f11982fffc0d7dd16b2cd2bc0e9b5322` |
| Assets/Editor/ShipyardDB/ShipyardPublicDataChecks.cs | `d4c28c3cb0f9c5ff6ffc93bec3474fda6e1ffa58cee77b6c296f0d2580f82d72` | `d4c28c3cb0f9c5ff6ffc93bec3474fda6e1ffa58cee77b6c296f0d2580f82d72` |
| Assets/Editor/Timing/CommandTimelinePlayModeChecks.cs | `19219866f6936180663a521e149a3be21d87349364437bec9c0ada63790b2f62` | `19219866f6936180663a521e149a3be21d87349364437bec9c0ada63790b2f62` |
| Assets/Editor/UI/OperatorCommandUIPlayModeChecks.cs | `5743d1958e52b434baa9e72a0a2cfd0108e51677966f5b5b93c454ad882d2fee` | `5743d1958e52b434baa9e72a0a2cfd0108e51677966f5b5b93c454ad882d2fee` |
| Assets/Editor/UI/ShipyardExploreChecks.cs | `fdef9fd7d7b1dadc4437dbd6a297054bb88aa3fd0cfdb194e903faed3ebb696a` | `fdef9fd7d7b1dadc4437dbd6a297054bb88aa3fd0cfdb194e903faed3ebb696a` |
| Assets/Editor/UI/ShipyardPlayChecks.cs | `170adfde3ff5b2f6e598e629cdc947da8453901d1b4798f832de0213cb0b30f4` | `170adfde3ff5b2f6e598e629cdc947da8453901d1b4798f832de0213cb0b30f4` |
| Assets/Editor/UI/TaskHmiPlayChecks.cs | `00481633ffd1ecd2f7fc222063610a669f60cfe1c5ea5761bb932fdb012ba613` | `00481633ffd1ecd2f7fc222063610a669f60cfe1c5ea5761bb932fdb012ba613` |
| Assets/Editor/UI/WorkerRouteRiskChecks.cs | `b3e4ac9d33a4eacf1d0e81a8be046151736442832dd341213e8c3ac90d88b643` | `b3e4ac9d33a4eacf1d0e81a8be046151736442832dd341213e8c3ac90d88b643` |

| 추가 파일 | 변경 전 | 변경 후 SHA-256 |
|---|---|---|
| Assets/Editor/UI/FacilityEntranceChecks.cs | 존재하지 않음 (N/A) | `f5a724648eb13609a93e88e652cfcfad357d29324bf4314ecec62212b90babc8` |
| Assets/Editor/UI/FacilityEntranceRegressionRunner.cs | 존재하지 않음 (N/A) | `c4b227a331b45b794417ec77bfd1027d3c520c054d025591c02134710716a631` |

자동생성 `.meta`는 test 실행 코드와 분리한다. 위 목록은 C# test/runner 파일이며, 모든 경로 앞에 `D:\UnityProject\CraneSignalAI\`가 붙는다. 증거: [TestRunnerHashes.json](../PostAudit/TestRunnerHashes.json). 이번 감사에서 이 테스트들을 재실행하거나 수정하지 않았다. Phase 1의 기존 기록은 original103 + confidence17 + walking44 + LiveLoad14 + timeline11 + Operator18 + HMI13 = 220 PASS이지만, 아래 fixture/harness 조건이 포함된 결과라는 제한이 있다.

### 2-1. LiveLoad Worker fixture 이동

원본 파일: `D:\UnityProject\CraneSignalAI\Assets\Editor\Prediction\LiveLoadPathPlayModeChecks.cs` 약 81행. **변경 전/후 원본 코드는 동일하다.**

```csharp
worker.transform.position=index==5?new Vector3(Mathf.Sin(20*Mathf.Deg2Rad)*3,0,Mathf.Cos(20*Mathf.Deg2Rad)*3):new Vector3(20,0,20);
```

변경은 기존 파일 안이 아니라 새 `D:\UnityProject\CraneSignalAI\Assets\Editor\UI\FacilityEntranceRegressionRunner.cs` 약 59행에서 이루어졌다. 실제 추가 diff(이 runner 자체가 Phase 1 전에는 없음):

```diff
--- /dev/null
+++ Assets/Editor/UI/FacilityEntranceRegressionRunner.cs
+ if (index == 5 && stage >= 1 && stage <= 6) GameObject.Find("Worker_01").transform.position = GameObject.Find("TowerCraneRoot").transform.position + new Vector3(Mathf.Sin(20 * Mathf.Deg2Rad) * 3, 0, Mathf.Cos(20 * Mathf.Deg2Rad) * 3);
```

기록된 이유는 legacy fixture가 조선소 이전 원점 기반이었다는 판단으로 보호 대상 LIVE root를 옮기지 않고 worker를 root 기준 20°/3 UU에 배치하려는 것이다. 그러나 **Phase 1 시작 전 실제 LIVE root도 이미 (-46,0,32)**였으므로 실제 baseline과 상대 배치를 보존한 이동은 아니다.

| 비교 항목 | 이동 전 | 새 runner 적용 후 |
|---|---:|---:|
| Worker world | (1.026060,0,2.819078) | (-44.973940,0,34.819078) |
| Worker − LIVE root | (47.026060,0,-29.180922) | (1.026060,0,2.819078) |
| LIVE root까지 거리 | 55.344165 UU | 3.000000 UU |
| baseline Hook (-46,6.5,35)까지 3D 거리 | 57.352525 UU | 6.582973 UU |
| baseline Hook까지 XZ 거리 | 56.982998 UU | 1.041889 UU |
| 같은 20° sweep 중심 (-44.973940,34.819078)까지 XZ 거리 | 56.035703 UU | 0 UU |

이동 벡터는 (-46,0,32), 크기는 56.035703 UU다. sweep 중심에서 멀리 떨어진 fixture를 경로 중심에 놓으므로 WorkerInRemainingArea/DANGER가 평가되는 기하 조건을 바꾼다. 표의 Hook 3D 거리는 **저장된 Scene baseline Hook 높이로 계산한 비교**이며 이동 전 legacy E-Worker 실행의 실제 관측값은 아니다. 당시 초기 legacy 실행은 앞 case에서 timeout되어 E-Worker 이동 전 관측 로그가 없다. XZ 비교는 HOIST 높이와 무관하다. 수치 증거: [LiveLoadFixtureGeometry.json](../PostAudit/LiveLoadFixtureGeometry.json).

expected value/판정 문구·숫자 변경은 없다. 기존 assertion의 실제 원본 줄은 다음과 같이 동일하다:

```csharp
if(index==5)Check(live.WorkerInRemainingArea && hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER,"Worker/full hazard/remaining debug disagree");
if(index==5)Check(hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER && !live.WorkerInRemainingArea,"Hazard meaning changed to Remaining");
```

기존 test의 “full hazard와 remaining 의미 구분”이라는 논리적 목표를 복원하려는 의도는 이해되지만, 사용자가 요구한 **변경 전/후 상대 거리 동일 기준으로는 의도 보존을 인정할 수 없다. 따라서 BLOCKER.** 원본 파일 해시가 같다는 사실만으로 동등한 테스트 실행이라고 보고하지 않는다.

같은 새 runner는 LiveLoad stage4/5 동안 held keyboard 이벤트를 재공급하고, Shipyard UI suite에서는 물리 Mouse를 임시 비활성화/복구하며, legacy Operator UI에서는 CommandPage와 해당 Canvas를 활성화하고 overview를 비활성화한다. 이것들도 expected 변경은 아니지만 입력·UI 실행 환경 변경이다. 전체 runner의 증거 사본 `FacilityEntranceRegressionRunner.source.txt` 및 실제 파일을 함께 검토해야 한다. 이번 감사에서는 runner를 실행하거나 고치지 않았다.

## 3. SDF.asset 범위 밖 변경

대상: `D:\UnityProject\CraneSignalAI\Assets\UI\Fonts\OperatorCommand Korean SDF.asset`

- 변경 전 SHA-256: `a20a9c424262c22a519805d4fccb631e8fd64b1b5348cfcfd680327be212f0a4`
- 변경 후 SHA-256: `80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4`
- glyph 3→71, **68개 추가**, character 3→71; 삭제 0, 기존 glyph 데이터 변경 0.
- 원본 폰트: `D:\UnityProject\CraneSignalAI\Assets\UI\Fonts\MalgunGothic.ttf`; SHA-256 `7a183cf1c6c56b9609fcc16eda8b5229fbc11758a21e669ec00343239b02192f` (이전/현재 동일).

변경 전 백업: `C:\Users\chlgu\Documents\Codex\2026-10-19\unity-mcp-main-scene-hierarchy-towercraneroot\outputs\FacilityEntrances\Backup\working-tree-overlay.zip` 내부 `Assets/UI/Fonts/OperatorCommand Korean SDF.asset`. 추출 사본: [OperatorCommand_Korean_SDF.before.asset](../PostAudit/OperatorCommand_Korean_SDF.before.asset). 현재 사본: [OperatorCommand_Korean_SDF.after.asset](../PostAudit/OperatorCommand_Korean_SDF.after.asset).

추가 문자 68개(정확한 code point):

감 (U+AC10), 값 (U+AC12), 강 (U+AC15), 경 (U+ACBD), 권 (U+AD8C), 기 (U+AE30), 깥 (U+AE65), 남 (U+B0A8), 내 (U+B0B4), 님 (U+B2D8), 달 (U+B2EC), 대 (U+B300), 도 (U+B3C4), 동 (U+B3D9), 든 (U+B4E0), 량 (U+B7C9), 령 (U+B839), 로 (U+B85C), 롤 (U+B864), 료 (U+B8CC), 리 (U+B9AC), 명 (U+BA85), 모 (U+BAA8), 목 (U+BAA9), 물 (U+BB3C), 미 (U+BBF8), 바 (U+BC14), 상 (U+C0C1), 선 (U+C120), 수 (U+C218), 승 (U+C2B9), 신 (U+C2E0), 아 (U+C544), 안 (U+C548), 않 (U+C54A), 업 (U+C5C5), 없 (U+C5C6), 예 (U+C608), 완 (U+C644), 우 (U+C6B0), 운 (U+C6B4), 위 (U+C704), 유 (U+C720), 은 (U+C740), 음 (U+C74C), 이 (U+C774), 인 (U+C778), 자 (U+C790), 작 (U+C791), 재 (U+C7AC), 전 (U+C804), 정 (U+C815), 좌 (U+C88C), 준 (U+C900), 지 (U+C9C0), 쪽 (U+CABD), 측 (U+CE21), 태 (U+D0DC), 트 (U+D2B8), 표 (U+D45C), 하 (U+D558), 해 (U+D574), 험 (U+D5D8), 현 (U+D604), 화 (U+D654), 확 (U+D655), 회 (U+D68C), 효 (U+D6A8).

Korean 하나만 변경된 것은 아니다. 전체 관련 폰트 비교:

| 폰트 | 변경 전 SHA-256 | 변경 후 SHA-256 | 추가 glyph |
|---|---|---|---|
| Assets/ShipyardDemo/ShipyardUI SDF.asset | `74ad1cfc25d3812d318295c51fc47513c27796fca3b52c796b6c03bf5baa5195` | `74ad1cfc25d3812d318295c51fc47513c27796fca3b52c796b6c03bf5baa5195` | 0 |
| Assets/UI/Fonts/CraneDashboard SDF.asset | `179166af489f32d532137ec3f0c324dbf410a7ec42e840f3da3122d8999a99d3` | `fe2fb3e62fa3bee4f0abbd1cbcb21f3ce3cd5336000586fd1a696fdb974f74c3` | 2 |
| Assets/UI/Fonts/OperatorCommand Korean SDF.asset | `a20a9c424262c22a519805d4fccb631e8fd64b1b5348cfcfd680327be212f0a4` | `80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4` | 68 |
| Assets/UI/Fonts/OperatorCommand SDF.asset | `4fbc9823c185b2c56e3395726cf0b67f2bdf862841dde1a614e203b4326089ac` | `a590f57ab9a76abc200d572d3f772e0f16d487d35a7dbba344686d07787f4429` | 29 |

Assets/UI/Fonts/CraneDashboard SDF.asset 추가 문자: 않 (U+C54A), 하 (U+D558).

Assets/UI/Fonts/OperatorCommand SDF.asset 추가 문자: ( (U+0028), ) (U+0029), J (U+004A), Q (U+0051), X (U+0058), [ (U+005B), ] (U+005D), b (U+0062), f (U+0066), y (U+0079), À (U+00C0), Á (U+00C1), Ã (U+00C3), Ê (U+00CA), Ò (U+00D2), Ó (U+00D3), Ô (U+00D4), Đ (U+0110), Ư (U+01AF), Ạ (U+1EA0), Ả (U+1EA2), Ậ (U+1EAC), Ể (U+1EC2), Ỉ (U+1EC8), Ờ (U+1EDC), Ợ (U+1EE2), Ụ (U+1EE4), Ủ (U+1EE6), Ự (U+1EF0).

세 폰트 모두 기존 glyph metrics/rect/index와 기존 character mapping은 동일하다. glyph/character/atlas 픽셀/packing 정보를 제외한 구조 비교 diff는 비어 있다. 폰트 크기·material/fallback/spacing 등 비글리프 설정 변경은 발견되지 않았다. raw diff와 구조 비교: [AllFontGlyphDiffs.json](../PostAudit/AllFontGlyphDiffs.json), `*_nonGlyph.diff`, `*_SDF.diff`. glyph/atlas 변화로 새로 지원되는 문자가 □/대체문자 대신 실제 글자로 표시되므로 해당 문구의 폭/줄바꿈이 달라질 가능성은 있다. pixel-perfect 무영향까지 주장하지 않는다. Dashboard와 OperatorCommand의 다른 화면에도 추가 문자 표시 영향은 있으나 기존 glyph 자체를 바꾼 증거는 없다.

Runtime logic와 localization: [BaselineCSharpComparison.json](../PostAudit/BaselineCSharpComparison.json)에서 Phase 1 전 대비 기존 C# 변경은 `Assets/Scripts/Shipyard/Explorer/ShipyardExploreCameraController.cs`, `Assets/Scripts/Shipyard/Explorer/ShipyardExploreHUD.cs` 이다. 이는 Phase 1 Entrance 연계 변경이며 폰트 수정으로 C#가 바뀐 것은 아니다. UI/Operator/localization 기존 C# 해시는 baseline과 동일하다. [LocalizationHashes.json](../PostAudit/LocalizationHashes.json)에 localization 코드/동반 파일을 별도 기록했다. 별도 localization 데이터 파일은 현재 Assets 검색에서 발견되지 않았다. 새 Entrance C#는 baseline에 없으므로 따로 취급한다.

Phase 1 compile PDB(보존된 파일 timestamp 2026-10-21 10:48:20 KST)의 SHA-256 document checksum과 현재 소스 90개가 모두 같다: [Phase1EndSourceComparison.json](../PostAudit/Phase1EndSourceComparison.json). 이는 컴파일된 소스와 현재 소스 일치를 증명하며 폰트 자체의 시간별 모든 상태를 증명하는 것은 아니다. 이번 감사 구간에도 전체 C# 불변이다.

되돌리는 방법(이번에는 실행하지 않음): 위 zip에서 **Korean SDF.asset 한 파일만** 원래 경로로 복구하고 기존 `.meta` GUID와 MalgunGothic.ttf는 유지한다. 그러면 추가된 68문자의 glyph 지원이 사라진다. Operator Command/Task HMI의 `운전`(기존 Phase 1 누락 경고 확인) 및 추가 문자에 의존하는 한국어 명령·상태 문구가 다시 대체문자/누락으로 보일 수 있다. 다른 fallback의 지원 여부에 따라 실제 누락 범위는 달라진다. Dashboard/OperatorCommand 추가 변경까지 완전 롤백하려면 해당 두 asset도 각각 백업에서 복구해야 하며 그때 위 +2/+29문자의 지원도 사라진다. 전체 zip을 덮어쓰면 무관한 변경도 되돌아가므로 금지할 것을 권고한다.

## 4. Scene / Entrance / LIVE / Fault / Console

최종 상태 snapshot: [AfterReproductionState.json](../PostAudit/AfterReproductionState.json). Main Scene은 기존 저장 파일을 연 상태이고 **dirty=false, Play=false, compiling=false, missing script=0**. 이번 감사에서 Scene 저장은 0회다. 저장된 Main.unity의 SHA-256은 `6462cb7d8199402ff495a4fefbbcdca8968452408a12f2362bcc9fcd2a80e929`; 감사 시작/종료 동일하다.

Entrance 소스의 Phase 1 compile checksum / 현재 SHA-256:

| 파일 | Phase 1 compile SHA-256 | 현재 SHA-256 |
|---|---|---|
| Assets/Scripts/Shipyard/Explorer/FacilityEntrance.cs | `af4ed6b2e019e6b2cdcbfb6dda4470f83dd314e38131ec8170c4e09a8109eda9` | `af4ed6b2e019e6b2cdcbfb6dda4470f83dd314e38131ec8170c4e09a8109eda9` |
| Assets/Scripts/Shipyard/Explorer/ShipyardExploreCameraController.cs | `1a7dc0be386a0239d4aefc505483f900b445fedf9cf57aafca07a80361b0b3e3` | `1a7dc0be386a0239d4aefc505483f900b445fedf9cf57aafca07a80361b0b3e3` |
| Assets/Scripts/Shipyard/Explorer/ShipyardExploreHUD.cs | `7221cccf43d762f465c3a065860bca6891b80dc75e3d95d5899a1c87c592b65a` | `7221cccf43d762f465c3a065860bca6891b80dc75e3d95d5899a1c87c592b65a` |
| Assets/Editor/UI/FacilityEntranceChecks.cs | `f5a724648eb13609a93e88e652cfcfad357d29324bf4314ecec62212b90babc8` | `f5a724648eb13609a93e88e652cfcfad357d29324bf4314ecec62212b90babc8` |
| Assets/Editor/UI/FacilityEntranceRegressionRunner.cs | `c4b227a331b45b794417ec77bfd1027d3c520c054d025591c02134710716a631` | `c4b227a331b45b794417ec77bfd1027d3c520c054d025591c02134710716a631` |
| Assets/Editor/UI/FacilityEntranceSetup.cs | `e2a7ddd347a8299029db126c67b20f58f08799e0e752ad50cce4069bf7712795` | `e2a7ddd347a8299029db126c67b20f58f08799e0e752ad50cce4069bf7712795` |

Entrance Mesh/Material/Scene은 감사 시작→종료 hash 동일([AuditStartHashes.json](../PostAudit/AuditStartHashes.json), [AuditEndHashes.json](../PostAudit/AuditEndHashes.json)). Phase 1 **종료 시점의 독립적인 Scene/새 Entrance asset SHA manifest는 없으므로**, 이들 비-C# asset이 Phase 1 종료 이후 단 한 번도 변경되지 않았다고 해시만으로 소급 증명할 수는 없다. Phase 1 FinalState/Preservation 기록과 현재 hierarchy 상태는 일치하며 현재 의도하지 않은 변경/missing script는 발견하지 못했다. Phase 1 전후 Scene 해시 차이는 Entrance 설치 자체에 따른 것이므로 감사 중 변경으로 혼동하지 않는다. 이 증거 한계는 남기고 미래 기준은 AuditStart/End로 고정한다.

LIVE: position `(-46,0,32)`, quaternion `(0,0,0,1)`, localScale `(1,1,1)`, parent `null`; 기대값 일치. TowerCraneController.enabled=true / CommandReceiver.enabled=true. Fault component.enabled=false / faultEnabled=false / faultMode=NONE; 기대값 일치.

Console snapshot의 **전체 Error 3 / Warning 11**을 0으로 축약하지 않는다. [AfterReproductionConsole.json](../PostAudit/AfterReproductionConsole.json)에는 메시지/전체 stack/file/line, [ConsoleClassification.json](../PostAudit/ConsoleClassification.json)에는 개별 분류를 기록했다.

| 구분 | Error | Warning | 근거/해석 |
|---|---:|---:|---|
| C# compiler | 0 | 0 | 현재 Console에 CS compiler 진단 없음, Phase 1 CleanCompile 기록과 일치; 이번 감사에서 강제 recompile하지 않음 |
| 프로젝트 Runtime/Editor | 0 | 0 | Assets/Scripts 또는 Assets/Editor 원인 프레임 없음 |
| MCP package / SignalR dependency | 3 | 10 | 300000ms server timeout 및 HubConnection reconnect/stop 오류 3개; disposed Flush / auto-reconnect-disabled warning 10개 |
| Unity native audio | 0 | 1 | 기본 오디오 장치 변경 후 초기화 실패/reset 시도; 프로젝트 script stack 없음 |

3개의 connection Error는 이번 Play 재현 전 snapshot에도 이미 존재한다. gate 2건은 원본 별도 로그이며 현재 Console count에 억지로 합산하지 않았다. 이전 정상 상태의 MCP warning 5건과 현재 10건은 같은 의미의 누적 개수라고 단정하지 않는다. 이번 재현 전후 warning은 6→11, Error 3→3이며 새 5 warning은 domain reload 시 같은 MCP lifecycle 종류다. native audio 1건은 재현 전에도 존재했다. 경고를 없애거나 filter하지 않았고 package 원인과 프로젝트 원인을 구분했다.

감사 입력 hash 606개 비교 결과 변경 0개: [AuditNoMutation.json](../PostAudit/AuditNoMutation.json). 이 범위는 C#, Editor Python, Scene, 폰트, Entrance asset, Packages, MCP package C#/JSON이며 전체 디스크의 byte-for-byte 검증이라는 뜻은 아니다. Unity가 자체 기록한 Library/Editor 로그 등은 증거 수집 대상이고 수정 금지 source/scene와 구분한다.

## 5. 승인과 다음 단계

**Phase 1을 그대로 승인해도 되는가? 아니오.** 기존 test 파일과 expected는 보존됐지만 새 runner의 Worker fixture 변경이 실제 baseline과 상대 거리/판정 조건을 바꾸었다. 사용자의 명시적 BLOCKER 기준에 해당한다. SDF 추가 변경은 CAUTION으로 기록하되 이 fixture 이슈를 상쇄하지 못한다.

**Phase 2 시작을 막는 BLOCKER가 있는가? 있다: LiveLoad fixture rebase의 테스트 동등성 불충족.** 해결하려면 별도 승인된 수정 작업에서 기존 기준의 테스트를 유지할지, 원점 기반 obsolete test를 scene-relative 테스트로 명시적으로 개정할지 결정하고, 변경 전/후 상대 기하·expected·입력 자극을 독립적으로 검증해야 한다. root/worker를 함께 같은 rigid transform으로 이동한 기준 비교 또는 변경을 분명히 선언한 새 테스트가 필요하다. 기존 220 PASS를 “실행 조건 불변의 legacy 회귀 통과”로 사용해서는 안 된다. 본 감사에서는 수정하거나 재테스트하여 문제를 덮지 않았다.

최종 read-only 재조회(2026-10-21 18:20:08 KST): [FinalReadOnlyState.json](../PostAudit/FinalReadOnlyState.json). Play Mode 종료, Scene dirty=false, missing script=0, Error 3 / Warning 11, LIVE/Fault 기대값 일치. Scene 저장 안 함. 보고 후 중단하며 Phase 2는 시작하지 않는다.
