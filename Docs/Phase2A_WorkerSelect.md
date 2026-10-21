# Phase 2A — Worker Select 기반 시스템 완료 보고

DEMO / SIMULATION. Phase 2A only.

## 1. Backup 결과

`Phase2A/Phase2_Start_Baseline/`에 baseline.bundle, working-tree-overlay.zip, Baseline.json(1522개 hash), GitStatus.txt를 보관했다. HEAD `5970d20427baf7106e3cda86b660c7da73e3252f`. 기존 미커밋 상태를 포함한 복원 기준이다. 새 commit/push 없음.

## 2. Legacy LiveLoad coverage map

| Legacy | 의미 | 기존 gate/누락 | 조치 |
|---|---|---|---|
| A-SLEW-MidMotion | Voice stationary; immutable full/target; actual decimation; remaining endpoints/current footprint/rotation | SceneRelative 8 partial; detailed sampling/endpoints gap | ADD G-SamplingEndpoints |
| A-SLEW-Completed | Remaining empty/zero; Full immutable | SceneRelative completion + Operator A-completed | COVERED |
| B-TROLLEY-MidMotion | Radial current/remaining shape + actual decimation | Operator B-moving reads values only; geometry gap | ADD G-TrolleyGeometry |
| B-TROLLEY-Completed | Numeric remaining zero; full snapshot unchanged | Operator B-completed + HMI actual/remaining partial | ADD G-TrolleyGeometry completion |
| C-HOIST-MidMotion | Vertical path; unchanged XZ footprint; current outline | Operator C moving value equality; XZ geometry gap | ADD G-HoistGeometry |
| C-HOIST-Completed | Remaining zero/empty + full preserved | Operator C completed | COVERED + appended completion |
| D-SmallLoad | Footprint area=2 and visual follows | SceneRelative fixed 1x2 corner check partial | ADD G-LoadDimensionScaling |
| D-LargeLoad | 4x2 area=8; 4x area; bounds growth >1.5; visual scale | No current Gate validates dimension-change response | ADD G-LoadDimensionScaling |
| E-Worker-MidMotion | Full DANGER and WorkerInRemainingArea simultaneously | SceneRelative positive + mid-motion and negative control | COVERED |
| E-Worker-Completed | Full DANGER remains after remaining empty + worker false | SceneRelative completed + STOP | COVERED |
| Wrap-MidMotion | 350→380 unwrapped target + remaining endpoints/orientation | No Gate specifically crosses 360 | ADD G-Wrap360 |
| Wrap-Completed | Unwrapped target reach + full preserved | No Gate specifically crosses 360 | ADD G-Wrap360 completion |
| WrongDirection | Opposite operator increases remaining; original target fixed | Timeline C tests mismatch, not increasing remaining | ADD G-WrongDirectionRemaining |
| STOP-History | Voice STOP clears full/remaining but retains footprint/history; operator Space STOP verified | Timeline H/Operator J/HMI Extra-Stop cover STOP; history retention gap | ADD G-StopHistory |
전체 실행 도달성은 `LegacyCoverageMap.json`, 구현 전 판단은 `Docs/Phase2A_Preflight.md`.

## 3. Gate coverage gap

기존 SceneRelative 8개/negative control을 그대로 두고 같은 파일 뒤에 별도 `LiveLoadSceneRelativeCoverage` 클래스의 8개 검사를 append했다: SamplingEndpoints, TrolleyGeometry, HoistGeometry, SmallLoadArea, LoadDimensionScaling, Wrap360, WrongDirectionRemaining, StopHistory. 원본 prefix 보존=True. 기존 legacy source/input/fixture/expected 변경 없음. 새 검사의 60Hz clock은 검증용이며 종료 시 복원한다. Legacy informational은 원래 clock을 사용한다.

## 4. Console 시작 baseline

Console을 지우지 않고 실제 누적 상태를 보존했다. 시작 총 Error19/Warning160. 과거 Editor sharing violation 1건도 시작 시 이미 존재했다. 패키지 로그와 프로젝트 원인을 구분한다.
```json
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
```


## 5. FPS 시작 baseline


```json
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
```
기존의 24FPS를 가정하지 않고 새로 측정한 baseline이다. Editor GameView 측정이며 standalone 성능 보장은 아니다.

## 6. WorkerProfile 구조

ScriptableObject 4개. workerId/roleCode/accidentRoleCode, KO/EN/VI 이름·설명, visual prefab, mainWorkTypes/mainHazards/workLocations/workFacilities, allowed/preferred/avoidZones, PPE, icon/color/pattern, DEMO candidate cost bias. allowedZones는 빈 배열이며 출입 허가로 사용하지 않는다. `Assets/Workers/Profiles/`.

## 7. WorkerRuntimeState 구조

작업자별 lastPosition/lastRotation/hasPosition, currentFacilityId/insideInteriorZoneId, activeRouteSessionId, routeSession, totalDistance, correction 기록. currentSessionRisk=null, reason=NOT_EVALUATED_PHASE_2A. inactive 작업자는 데이터만 유지하며 이동·샘플링하지 않는다.

## 8. 기존 role 구조 재사용/확장

기존 WorkerRoute/ExplorerHazard/Worker_01/Prediction 코드는 유지했다. WorkerRuntimeAgent가 기존 Explorer hazard 결과에 active worker ID/role을 붙이는 read-only context adapter다. 기존 direct/detour planner를 새 알고리즘으로 교체하지 않았다. 기존 LIVE Worker_01의 별도 위험 검증 대상을 임의로 옮기지 않았다.

## 9. Accident role 실제 DB 매핑

실제 normalized JSON의 workerRoleCode와 codebook CM-H04를 확인했다. SIGNALER→2(35사례), RIGGER→3(65사례). WELDER_FITTER/OUTFITTING_PIPE→NOT_MAPPED; 해당 UI는 역할 기반 사고 필터 데이터 없음. 임의로 CM-H04=5를 쓰지 않는다. exact code 검색이며 확률이나 유사도 점수가 아니다.
```json
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
```


## 10. Hierarchy

기존 ExplorerPlayer
└ WorkerRuntimeAgent(component)
└ WorkerAvatarRoot
  └ ActiveWorkerVisual(선택 prefab)

새 WorkerSelectionSystem: WorkerSelectUI + WorkerRouteRenderer. Runtime Canvas/시각 경로 mesh/label은 여기에 생성한다. Worker Select 중앙 미리보기도 기존 Explore 카메라를 사용한다. Player/CharacterController/Camera 복제 없음. CC h1.8/r.28/centerY.9/step.25, eye1.7 유지.

## 11. Avatar / PPE

프로시저럴 visual-only prefab 4개. 공통 성인 비율 작업복/안전모/안전화/장갑/반사띠. 신호수: 조끼/무전기/호루라기. 줄걸이: 하네스/태그라인. 용접·취부: 차광면/앞치마/가죽장갑/마스크/토치. 의장·배관: 공구벨트/렌치/배관 도구. 외부 다운로드·상표·개인 얼굴 없음. prefab에 컨트롤러/콜라이더/이동 script 없음.

## 12. Worker Select UI 및 이용법

Play→Facility 선택→Explore 진입→Tab으로 커서 해제→CHANGE WORKER(작업자 변경). 카드 선택→SELECT(현재 작업자로 선택)로 현재 분석 작업자 전환. KO/EN/VI 버튼으로 언어 변경. CURRENT는 현재 작업자, ALL은 작업자 ID/아이콘/선 패턴/색상으로 모든 DEMO 경로를 표시한다. ALL은 서로 다른 기록 시간의 겹쳐보기임을 명시한다. EXPORT는 실제 파일 생성, CLEAR는 현재 session만 초기화한다. Modal 동안 WASD/E/F/Operator 입력 누출을 막으며 닫으면 원래 Explore 정책으로 돌아간다. 3D preview/역할 카드/선택 상태/사고 필터/Session Risk 미계산 상태 표시. Screenshots: PostGate/09_Workers/.

## 13. KO/EN/VI 데이터

WorkerUiStrings.json의 key 기반 세 언어 catalog + profile의 세 언어 이름/설명. 4 roles×3언어=12 페이지 TMP coverage 확인. 베트남어는 DEMO 번역이며 현장 용어 검수는 별도 권장. Runtime localization 전면 교체 없음.

## 14. Font 검사

허용된 기존 font 3종 중 OperatorCommand Korean SDF +138 glyph, OperatorCommand SDF +40 glyph; CraneDashboard 변경 없음. 최종 old glyph/폰트 자산 기타 설정/다른 폰트 변경 없음. Scene 저장으로 동적 TMP runtime 문자가 함께 저장된 중간 이력은 SaveSceneFontDrift/에 보존했다. 시작 font에서 기존 WorkerFontChanges allow-list만 다시 추가하여 ValidatedFonts/에 고정했고, 최종 파일은 이 검증본으로 복원했다. 이 폰트 검사는 HMI text component의 auto-size 최소값과는 별도다. 문자·Unicode·사용 문구·화면은 WorkerFontMissingBefore.json, WorkerUIUsedPhrases.json. 각 before/after SHA256과 추가문자 전체 목록은 아래 및 FontAudit.json.
```json
{
  "allowedFonts": [
    {
      "path": "Assets/UI/Fonts/CraneDashboard SDF.asset",
      "before": "fe2fb3e62fa3bee4f0abbd1cbcb21f3ce3cd5336000586fd1a696fdb974f74c3",
      "after": "fe2fb3e62fa3bee4f0abbd1cbcb21f3ce3cd5336000586fd1a696fdb974f74c3",
      "addedGlyphCount": 0,
      "addedCharacters": [],
      "nonWorkerCharacters": [],
      "oldGlyphChanges": [],
      "removedCharacters": [],
      "settingsUnchanged": true
    },
    {
      "path": "Assets/UI/Fonts/OperatorCommand SDF.asset",
      "before": "a590f57ab9a76abc200d572d3f772e0f16d487d35a7dbba344686d07787f4429",
      "after": "2a64331499f437b5fcc4e323e9c5747703811a06fce0ead327a48e31247dc38c",
      "addedGlyphCount": 40,
      "addedCharacters": [
        {
          "character": "ă",
          "unicode": "U+0103"
        },
        {
          "character": "đ",
          "unicode": "U+0111"
        },
        {
          "character": "Ơ",
          "unicode": "U+01A0"
        },
        {
          "character": "ơ",
          "unicode": "U+01A1"
        },
        {
          "character": "ư",
          "unicode": "U+01B0"
        },
        {
          "character": "ạ",
          "unicode": "U+1EA1"
        },
        {
          "character": "ả",
          "unicode": "U+1EA3"
        },
        {
          "character": "Ấ",
          "unicode": "U+1EA4"
        },
        {
          "character": "ấ",
          "unicode": "U+1EA5"
        },
        {
          "character": "ầ",
          "unicode": "U+1EA7"
        },
        {
          "character": "ẩ",
          "unicode": "U+1EA9"
        },
        {
          "character": "ẫ",
          "unicode": "U+1EAB"
        },
        {
          "character": "ậ",
          "unicode": "U+1EAD"
        },
        {
          "character": "ắ",
          "unicode": "U+1EAF"
        },
        {
          "character": "ặ",
          "unicode": "U+1EB7"
        },
        {
          "character": "ẹ",
          "unicode": "U+1EB9"
        },
        {
          "character": "ế",
          "unicode": "U+1EBF"
        },
        {
          "character": "ề",
          "unicode": "U+1EC1"
        },
        {
          "character": "ể",
          "unicode": "U+1EC3"
        },
        {
          "character": "ệ",
          "unicode": "U+1EC7"
        },
        {
          "character": "ị",
          "unicode": "U+1ECB"
        },
        {
          "character": "Ọ",
          "unicode": "U+1ECC"
        },
        {
          "character": "ọ",
          "unicode": "U+1ECD"
        },
        {
          "character": "ỏ",
          "unicode": "U+1ECF"
        },
        {
          "character": "Ố",
          "unicode": "U+1ED0"
        },
        {
          "character": "ố",
          "unicode": "U+1ED1"
        },
        {
          "character": "ồ",
          "unicode": "U+1ED3"
        },
        {
          "character": "Ổ",
          "unicode": "U+1ED4"
        },
        {
          "character": "ỗ",
          "unicode": "U+1ED7"
        },
        {
          "character": "ộ",
          "unicode": "U+1ED9"
        },
        {
          "character": "Ớ",
          "unicode": "U+1EDA"
        },
        {
          "character": "ờ",
          "unicode": "U+1EDD"
        },
        {
          "character": "ở",
          "unicode": "U+1EDF"
        },
        {
          "character": "ợ",
          "unicode": "U+1EE3"
        },
        {
          "character": "ụ",
          "unicode": "U+1EE5"
        },
        {
          "character": "ủ",
          "unicode": "U+1EE7"
        },
        {
          "character": "ứ",
          "unicode": "U+1EE9"
        },
        {
          "character": "Ữ",
          "unicode": "U+1EEE"
        },
        {
          "character": "ữ",
          "unicode": "U+1EEF"
        },
        {
          "character": "ự",
          "unicode": "U+1EF1"
        }
      ],
      "nonWorkerCharacters": [],
      "oldGlyphChanges": [],
      "removedCharacters": [],
      "settingsUnchanged": true
    },
    {
      "path": "Assets/UI/Fonts/OperatorCommand Korean SDF.asset",
      "before": "80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4",
      "after": "3bb7c2969bd97cc8e6f484f62369dba37bd0bdf7069bb78a37dccc0ce426e44b",
      "addedGlyphCount": 138,
      "addedCharacters": [
        {
          "character": "가",
          "unicode": "U+AC00"
        },
        {
          "character": "각",
          "unicode": "U+AC01"
        },
        {
          "character": "간",
          "unicode": "U+AC04"
        },
        {
          "character": "갑",
          "unicode": "U+AC11"
        },
        {
          "character": "거",
          "unicode": "U+AC70"
        },
        {
          "character": "건",
          "unicode": "U+AC74"
        },
        {
          "character": "걸",
          "unicode": "U+AC78"
        },
        {
          "character": "검",
          "unicode": "U+AC80"
        },
        {
          "character": "결",
          "unicode": "U+ACB0"
        },
        {
          "character": "겹",
          "unicode": "U+ACB9"
        },
        {
          "character": "고",
          "unicode": "U+ACE0"
        },
        {
          "character": "공",
          "unicode": "U+ACF5"
        },
        {
          "character": "관",
          "unicode": "U+AD00"
        },
        {
          "character": "광",
          "unicode": "U+AD11"
        },
        {
          "character": "구",
          "unicode": "U+AD6C"
        },
        {
          "character": "규",
          "unicode": "U+ADDC"
        },
        {
          "character": "그",
          "unicode": "U+ADF8"
        },
        {
          "character": "근",
          "unicode": "U+ADFC"
        },
        {
          "character": "깅",
          "unicode": "U+AE45"
        },
        {
          "character": "끼",
          "unicode": "U+B07C"
        },
        {
          "character": "낙",
          "unicode": "U+B099"
        },
        {
          "character": "네",
          "unicode": "U+B124"
        },
        {
          "character": "다",
          "unicode": "U+B2E4"
        },
        {
          "character": "단",
          "unicode": "U+B2E8"
        },
        {
          "character": "닫",
          "unicode": "U+B2EB"
        },
        {
          "character": "당",
          "unicode": "U+B2F9"
        },
        {
          "character": "더",
          "unicode": "U+B354"
        },
        {
          "character": "던",
          "unicode": "U+B358"
        },
        {
          "character": "데",
          "unicode": "U+B370"
        },
        {
          "character": "되",
          "unicode": "U+B418"
        },
        {
          "character": "드",
          "unicode": "U+B4DC"
        },
        {
          "character": "딪",
          "unicode": "U+B52A"
        },
        {
          "character": "라",
          "unicode": "U+B77C"
        },
        {
          "character": "락",
          "unicode": "U+B77D"
        },
        {
          "character": "래",
          "unicode": "U+B798"
        },
        {
          "character": "러",
          "unicode": "U+B7EC"
        },
        {
          "character": "레",
          "unicode": "U+B808"
        },
        {
          "character": "렌",
          "unicode": "U+B80C"
        },
        {
          "character": "례",
          "unicode": "U+B840"
        },
        {
          "character": "록",
          "unicode": "U+B85D"
        },
        {
          "character": "루",
          "unicode": "U+B8E8"
        },
        {
          "character": "른",
          "unicode": "U+B978"
        },
        {
          "character": "를",
          "unicode": "U+B97C"
        },
        {
          "character": "린",
          "unicode": "U+B9B0"
        },
        {
          "character": "립",
          "unicode": "U+B9BD"
        },
        {
          "character": "링",
          "unicode": "U+B9C1"
        },
        {
          "character": "마",
          "unicode": "U+B9C8"
        },
        {
          "character": "만",
          "unicode": "U+B9CC"
        },
        {
          "character": "맞",
          "unicode": "U+B9DE"
        },
        {
          "character": "며",
          "unicode": "U+BA70"
        },
        {
          "character": "면",
          "unicode": "U+BA74"
        },
        {
          "character": "무",
          "unicode": "U+BB34"
        },
        {
          "character": "묶",
          "unicode": "U+BB36"
        },
        {
          "character": "반",
          "unicode": "U+BC18"
        },
        {
          "character": "배",
          "unicode": "U+BC30"
        },
        {
          "character": "밸",
          "unicode": "U+BC38"
        },
        {
          "character": "버",
          "unicode": "U+BC84"
        },
        {
          "character": "번",
          "unicode": "U+BC88"
        },
        {
          "character": "베",
          "unicode": "U+BCA0"
        },
        {
          "character": "벨",
          "unicode": "U+BCA8"
        },
        {
          "character": "변",
          "unicode": "U+BCC0"
        },
        {
          "character": "별",
          "unicode": "U+BCC4"
        },
        {
          "character": "보",
          "unicode": "U+BCF4"
        },
        {
          "character": "부",
          "unicode": "U+BD80"
        },
        {
          "character": "분",
          "unicode": "U+BD84"
        },
        {
          "character": "붐",
          "unicode": "U+BD90"
        },
        {
          "character": "브",
          "unicode": "U+BE0C"
        },
        {
          "character": "블",
          "unicode": "U+BE14"
        },
        {
          "character": "비",
          "unicode": "U+BE44"
        },
        {
          "character": "사",
          "unicode": "U+C0AC"
        },
        {
          "character": "서",
          "unicode": "U+C11C"
        },
        {
          "character": "석",
          "unicode": "U+C11D"
        },
        {
          "character": "설",
          "unicode": "U+C124"
        },
        {
          "character": "세",
          "unicode": "U+C138"
        },
        {
          "character": "션",
          "unicode": "U+C158"
        },
        {
          "character": "속",
          "unicode": "U+C18D"
        },
        {
          "character": "송",
          "unicode": "U+C1A1"
        },
        {
          "character": "스",
          "unicode": "U+C2A4"
        },
        {
          "character": "슬",
          "unicode": "U+C2AC"
        },
        {
          "character": "시",
          "unicode": "U+C2DC"
        },
        {
          "character": "실",
          "unicode": "U+C2E4"
        },
        {
          "character": "앙",
          "unicode": "U+C559"
        },
        {
          "character": "앞",
          "unicode": "U+C55E"
        },
        {
          "character": "양",
          "unicode": "U+C591"
        },
        {
          "character": "에",
          "unicode": "U+C5D0"
        },
        {
          "character": "역",
          "unicode": "U+C5ED"
        },
        {
          "character": "외",
          "unicode": "U+C678"
        },
        {
          "character": "요",
          "unicode": "U+C694"
        },
        {
          "character": "용",
          "unicode": "U+C6A9"
        },
        {
          "character": "워",
          "unicode": "U+C6CC"
        },
        {
          "character": "으",
          "unicode": "U+C73C"
        },
        {
          "character": "의",
          "unicode": "U+C758"
        },
        {
          "character": "임",
          "unicode": "U+C784"
        },
        {
          "character": "장",
          "unicode": "U+C7A5"
        },
        {
          "character": "저",
          "unicode": "U+C800"
        },
        {
          "character": "적",
          "unicode": "U+C801"
        },
        {
          "character": "절",
          "unicode": "U+C808"
        },
        {
          "character": "점",
          "unicode": "U+C810"
        },
        {
          "character": "접",
          "unicode": "U+C811"
        },
        {
          "character": "제",
          "unicode": "U+C81C"
        },
        {
          "character": "조",
          "unicode": "U+C870"
        },
        {
          "character": "존",
          "unicode": "U+C874"
        },
        {
          "character": "주",
          "unicode": "U+C8FC"
        },
        {
          "character": "죽",
          "unicode": "U+C8FD"
        },
        {
          "character": "줄",
          "unicode": "U+C904"
        },
        {
          "character": "중",
          "unicode": "U+C911"
        },
        {
          "character": "차",
          "unicode": "U+CC28"
        },
        {
          "character": "착",
          "unicode": "U+CC29"
        },
        {
          "character": "체",
          "unicode": "U+CCB4"
        },
        {
          "character": "쳐",
          "unicode": "U+CCD0"
        },
        {
          "character": "추",
          "unicode": "U+CD94"
        },
        {
          "character": "축",
          "unicode": "U+CD95"
        },
        {
          "character": "취",
          "unicode": "U+CDE8"
        },
        {
          "character": "치",
          "unicode": "U+CE58"
        },
        {
          "character": "칙",
          "unicode": "U+CE59"
        },
        {
          "character": "크",
          "unicode": "U+D06C"
        },
        {
          "character": "타",
          "unicode": "U+D0C0"
        },
        {
          "character": "탈",
          "unicode": "U+D0C8"
        },
        {
          "character": "택",
          "unicode": "U+D0DD"
        },
        {
          "character": "터",
          "unicode": "U+D130"
        },
        {
          "character": "토",
          "unicode": "U+D1A0"
        },
        {
          "character": "파",
          "unicode": "U+D30C"
        },
        {
          "character": "판",
          "unicode": "U+D310"
        },
        {
          "character": "팔",
          "unicode": "U+D314"
        },
        {
          "character": "평",
          "unicode": "U+D3C9"
        },
        {
          "character": "프",
          "unicode": "U+D504"
        },
        {
          "character": "피",
          "unicode": "U+D53C"
        },
        {
          "character": "필",
          "unicode": "U+D544"
        },
        {
          "character": "할",
          "unicode": "U+D560"
        },
        {
          "character": "항",
          "unicode": "U+D56D"
        },
        {
          "character": "허",
          "unicode": "U+D5C8"
        },
        {
          "character": "협",
          "unicode": "U+D611"
        },
        {
          "character": "형",
          "unicode": "U+D615"
        },
        {
          "character": "호",
          "unicode": "U+D638"
        },
        {
          "character": "홀",
          "unicode": "U+D640"
        },
        {
          "character": "후",
          "unicode": "U+D6C4"
        },
        {
          "character": "흄",
          "unicode": "U+D744"
        },
        {
          "character": "힘",
          "unicode": "U+D798"
        }
      ],
      "nonWorkerCharacters": [],
      "oldGlyphChanges": [],
      "removedCharacters": [],
      "settingsUnchanged": true
    }
  ],
  "otherFonts": [
    {
      "path": "Assets/ShipyardDemo/ShipyardUI SDF.asset",
      "before": "74ad1cfc25d3812d318295c51fc47513c27796fca3b52c796b6c03bf5baa5195",
      "after": "74ad1cfc25d3812d318295c51fc47513c27796fca3b52c796b6c03bf5baa5195",
      "unchanged": true
    },
    {
      "path": "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset",
      "before": "f53586850a70c4f308e7e71b99b63881a49f38de4bd40f4edb7fe7ae6ecf8a52",
      "after": "f53586850a70c4f308e7e71b99b63881a49f38de4bd40f4edb7fe7ae6ecf8a52",
      "unchanged": true
    },
    {
      "path": "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
      "before": "c3ff50e3133d989180b187748ffc67903f1fede099a5b85b4fcbd8cb6e8be958",
      "after": "c3ff50e3133d989180b187748ffc67903f1fede099a5b85b4fcbd8cb6e8be958",
      "unchanged": true
    },
    {
      "path": "PostAudit/OperatorCommand_Korean_SDF.after.asset",
      "before": "80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4",
      "after": "80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4",
      "unchanged": true
    },
    {
      "path": "PostAudit/OperatorCommand_Korean_SDF.before.asset",
      "before": "a20a9c424262c22a519805d4fccb631e8fd64b1b5348cfcfd680327be212f0a4",
      "after": "a20a9c424262c22a519805d4fccb631e8fd64b1b5348cfcfd680327be212f0a4",
      "unchanged": true
    }
  ],
  "passed": true
}
```


## 15. lastPosition 저장/복원

전환 전 active state 저장→다음 worker 저장 위치 검증→기존 Explorer의 Teleport 경로로 안전 복원. 최초 방문 작업자는 현재 위치에서 시작. 기존 Player/CC/Camera identity 유지. A→B→A 실제 오차:
```json
{
  "expected": [
    -51.0,
    -5.58793545e-08,
    -36.0771141
  ],
  "actual": [
    -51.0,
    -1.15484e-07,
    -36.0771141
  ],
  "errorM": 5.96046448e-08
}
```
작업자 전환/re-entry의 위치 점프는 route break로 표시하고 보행거리에 더하지 않는다. 저장은 현재 Play session 메모리이며 앱 재시작 persistent restore는 이번 범위가 아니다.

## 16. Unsafe fallback

실제 geometry를 이동하지 않고 inactive saved data만 wall/sea/door frame 위치로 주입했다. 기존 ground/solid capsule과 문턱 검증→0.25m ring 32방향으로 최대3m→기존 facility validated spawn→기본 F01 validated spawn. 모두 실패하면 전환 거부. 보정 reason/requested/restored 기록.
```json
[
  {
    "kind": "WALL",
    "requested": [
      -53.8799973,
      -1.15484e-07,
      -31.0
    ],
    "restored": [
      -54.07134,
      0.075,
      -31.46194
    ],
    "distanceXZ": 0.5000008,
    "reason": "NEAREST_WALKABLE_WITHIN_3M"
  },
  {
    "kind": "SEA",
    "requested": [
      110.0,
      0.0,
      110.0
    ],
    "restored": [
      -51.0,
      0.004999999,
      -35.0
    ],
    "distanceXZ": 216.670258,
    "reason": "FACILITY_VALIDATED_SPAWN"
  },
  {
    "kind": "DOOR_FRAME",
    "requested": [
      -54.0399971,
      0.04,
      -31.0
    ],
    "restored": [
      -54.23134,
      0.075,
      -31.46194
    ],
    "distanceXZ": 0.5000008,
    "reason": "NEAREST_WALKABLE_WITHIN_3M"
  }
]
```


## 17. WorkerRouteSession

worker/session ID,start/end, sampledPositions(position,UTC,elapsed,facility,load,stage,break), visitedFacilities/visitedRiskZones,distanceM,totalSamples,decimationPasses. 0.5초 또는1m 샘플; 최대2000점, 초과 시 과거 점 감축/첫·끝점 보존. 현재 역할만 샘플링. exposureTime=null/highestRisk=NOT_EVALUATED; nearMiss/authorized collection은 빈 값. craneStage=NOT_EVALUATED, stable load ID 없음→NOT_AVAILABLE. 위험 계산/near-miss rule/exclusion zone 미구현.
```json
{
  "isolated": true,
  "pointCap": 2000,
  "retained": 1005,
  "decimationPasses": 3
}
```


## 18. CURRENT / ALL

4개 pooled route renderer와 cached mesh, 작업자 ID/icon/서로 다른4패턴. active 경로만/전체를 UI로 전환. route hover는 기록 당시 worker/time/facility/load/stage를 보여준다. 과거 stage 재추론 없음. ALL DEMO 시간 겹침 문구 검증. 비활성 worker별 player 없음.
```json
{
  "workers": 4,
  "labels": [
    "WKR_SIGNAL_01",
    "WKR_RIG_01",
    "WKR_WELD_01",
    "WKR_OUTFIT_01"
  ],
  "patterns": [
    "- - - -",
    "- . - .",
    "--------",
    ". . . ."
  ]
}
```


## 19. DEMO route preference

기존 planner는 authored direct/detour 배열이며 role preference 기반 solver가 없다. 이번에는 preference data + 기존 후보에 대한 cost bias 평가까지만 구현했다. 자동 우회 경로 생성이나 실제 역할별 routing 완료라고 주장하지 않는다.
```json
{
  "plannerSupportsRolePreference": false,
  "newPathfinding": false,
  "signalCost": 2.0,
  "welderCost": 1.4
}
```


## 20. EXPORT 경로/예시

실제 출력: `C:\Users\chlgu\AppData\LocalLow\DefaultCompany\CraneSignalAI\WorkerRoutes\20261021_150344_486`. JSON4/CSV4 검증. 복사 증거 `Phase2A/ExportEvidence/`. workerId/roleCode/sessionId/generated timestamp/DEMO/1m per UU metadata. 세계 좌표 x/y/z 직접 export; GIS 재변환 없음.
`C:\Users\chlgu\AppData\LocalLow\DefaultCompany\CraneSignalAI\WorkerRoutes\20261021_150344_486\WKR_OUTFIT_01_WKR_OUTFIT_01-0c76c0d047ab4cacafdeefb6c79dd765.json`
```json
{
  "metadata": {
    "data_status": "DEMO / SIMULATION",
    "generated_timestamp": "2026-10-21T15:03:44.7173290Z",
    "operational_meters_per_UU": 1,
```
`C:\Users\chlgu\AppData\LocalLow\DefaultCompany\CraneSignalAI\WorkerRoutes\20261021_150344_486\WKR_OUTFIT_01_WKR_OUTFIT_01-0c76c0d047ab4cacafdeefb6c79dd765.csv`
```csv
# DEMO / SIMULATION
# generated_timestamp=2026-10-21T15:03:44.7173290Z
# operational_meters_per_UU=1
# workerId=WKR_OUTFIT_01,roleCode=OUTFITTING_PIPE,sessionId=WKR_OUTFIT_01-0c76c0d047ab4cacafdeefb6c79dd765
workerId,roleCode,sessionId,timestampUtc,x,y,z,facilityId,loadId,craneStage,breakBefore,data_status
```

## 21. 신규/수정 파일

전체 경로와 SHA256: FileAudit.json. 기존 수정은 Explorer controller의 modal/복원 최소 hook 및 종료 lifecycle guard, Main.unity의 Worker objects/refs와 RemainingText 자동크기 하한 27.2→20, 허용 font 2개, append-only SceneRelative coverage. 신규 Runtime5개(WorkerProfile,WorkerRuntimeAgent,WorkerSelectUI,WorkerRouteRenderer,WorkerRouteExport), Editor3개(Setup,Runner,PlayModeChecks), Profiles4/Avatars4/공유Meshes·Materials/문구JSON. 기존 test/runner 중 SceneRelative append 예외 외 변경 없음. legacy/Entrance/TCP/Voice/Crane/Verification/Fault/Prediction/DB/Packages hash 유지. Explorer.diff 제공.
```json
[
  {
    "path": "Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs",
    "before": "d28023582f00fe4da3de0485f6ee710b0f53beab67bfeb77a567b8c7bcd9a348",
    "after": "5f84a49a48ac6c864d1981b1ca91a7ef25f1aa2c48d42e1055dd97e0706c0a03"
  },
  {
    "path": "Assets/Scenes/Main.unity",
    "before": "6462cb7d8199402ff495a4fefbbcdca8968452408a12f2362bcc9fcd2a80e929",
    "after": "9b9637efef2f140789abaecb795b778c72ec9b84d9136d06f9ea0937735e9cc4"
  },
  {
    "path": "Assets/Scripts/Shipyard/Explorer/ShipyardExploreCameraController.cs",
    "before": "1a7dc0be386a0239d4aefc505483f900b445fedf9cf57aafca07a80361b0b3e3",
    "after": "d51fb2f5fe6bc6b15e4d0246414fc934455db8a550b826d689496240a30d384a"
  },
  {
    "path": "Assets/UI/Fonts/OperatorCommand Korean SDF.asset",
    "before": "80cdc5863dbfc01e632f9096af7cf72b720d1def247ce778c59d13aa5eeab4b4",
    "after": "3bb7c2969bd97cc8e6f484f62369dba37bd0bdf7069bb78a37dccc0ce426e44b"
  },
  {
    "path": "Assets/UI/Fonts/OperatorCommand SDF.asset",
    "before": "a590f57ab9a76abc200d572d3f772e0f16d487d35a7dbba344686d07787f4429",
    "after": "2a64331499f437b5fcc4e323e9c5747703811a06fce0ead327a48e31247dc38c"
  }
]
```


## 22. 기존 Gate 결과

각 suite fresh Play; 기존 expected 수정 없음. HMI 재검증에서 기존 RemainingText의 TARGET REACHED 잘림을 발견했다. 200×48 rect에서 하한27.2와24는 잘렸고 하한20에서 실제21.9px로 맞는다(HmiRemainingFit_Diagnostic.json). Main의 해당 TMP component min-size 한 필드만 수정했다(HmiAutoSizeFix.json, SceneObjectDiff.json). 폰트 자산·glyph·spacing·material·fallback 설정은 유지했다. HmiRepairGate의 HMI13/Operator18이 최종 결과이며, 원래 실패 JSON은 PostGate/07_HMI13에 보존했다. 기능 Gate는 동일 Runtime 코드로 전부 재실행한 결과를 사용하며, 이후 이 텍스트 한 필드 수정에 영향을 받는 HMI/Operator 화면은 다시 검증했다. 원본103, confidence17, Entrance44, SceneRelative8(negative포함), Timeline11, Operator18, HMI13 및 추가coverage8 검사 결과:
```json
[
  {
    "suite": "00_Original103",
    "files": [
      {
        "path": "Phase2A/PostGate/00_Original103/AccidentEvidence/Core_tests.json",
        "passed": true,
        "count": 12,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/AccidentEvidence/Play_tests.json",
        "passed": true,
        "count": 13,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/DB/DB_Play_tests.json",
        "passed": true,
        "count": 16,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/Edit/Structure_tests.json",
        "passed": true,
        "count": 15,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/Explore/Explore_tests.json",
        "passed": true,
        "count": 17,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/Regression/Shipyard_tests.json",
        "passed": true,
        "count": 19,
        "error": null
      },
      {
        "path": "Phase2A/PostGate/00_Original103/WorkerRoute/WorkerRoute_tests.json",
        "passed": true,
        "count": 11,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "01_Confidence17",
    "files": [
      {
        "path": "Phase2A/PostGate/01_Confidence17/Confidence_tests.json",
        "passed": true,
        "count": 17,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "02_Entrance44",
    "files": [
      {
        "path": "Phase2A/PostGate/02_Entrance44/Entrance_tests.json",
        "passed": true,
        "count": 44,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "03_SceneRelative8",
    "files": [
      {
        "path": "Phase2A/PostGate/03_SceneRelative8/SceneRelative_tests.json",
        "passed": true,
        "count": 8,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "04_CoverageGaps",
    "files": [
      {
        "path": "Phase2A/PostGate/04_CoverageGaps/SceneRelativeCoverage_tests.json",
        "passed": true,
        "count": 8,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "05_Timeline11",
    "files": [
      {
        "path": "Phase2A/PostGate/05_Timeline11/D3_Phase3_tests.json",
        "passed": true,
        "count": 11,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "06_Operator18",
    "files": [
      {
        "path": "Phase2A/HmiRepairGate/01_Operator18/D4_tests.json",
        "passed": true,
        "count": 18,
        "error": null
      }
    ],
    "harnessErrors": [],
    "previousAttemptFiles": [
      {
        "path": "Phase2A/PostGate/06_Operator18/D4_tests.json",
        "passed": true,
        "count": 18,
        "error": null
      }
    ],
    "repairReason": "RemainingText existing English TARGET REACHED truncation; only Scene TMP auto-size minimum adjusted; no test/expected/font asset settings changed"
  },
  {
    "suite": "07_HMI13",
    "files": [
      {
        "path": "Phase2A/HmiRepairGate/00_HMI13/HMI_tests.json",
        "passed": true,
        "count": 13,
        "error": null
      }
    ],
    "harnessErrors": [],
    "previousAttemptFiles": [
      {
        "path": "Phase2A/PostGate/07_HMI13/HMI_tests.json",
        "passed": false,
        "count": 7,
        "error": "System.Exception: Stage 9: Truncated text RemainingText in COMMAND: TARGET REACHED\r\n  at TaskHmiPlayChecks.Require (System.Boolean condition, System.String message) [0x00023] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\UI\\TaskHmiPlayChecks.cs:55 \r\n  at TaskHmiPlayChecks.LayoutCheck () [0x0004f] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\UI\\TaskHmiPlayChecks.cs:82 \r\n  at TaskHmiPlayChecks.Tick () [0x0067c] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\UI\\TaskHmiPlayChecks.cs:142 "
      }
    ],
    "repairReason": "RemainingText existing English TARGET REACHED truncation; only Scene TMP auto-size minimum adjusted; no test/expected/font asset settings changed"
  },
  {
    "suite": "08_LegacyInfo",
    "files": [
      {
        "path": "Phase2A/PostGate/08_LegacyInfo/LiveLoad_tests.json",
        "passed": false,
        "count": 2,
        "error": "System.Exception: B-TROLLEY: Committed a pose every frame\r\n  at LiveLoadPathPlayModeChecks.Check (System.Boolean ok, System.String message) [0x0002d] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:69 \r\n  at LiveLoadPathPlayModeChecks.Tick () [0x005f5] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:118 "
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "09_Workers",
    "files": [
      {
        "path": "Phase2A/PostGate/09_Workers/Phase2A_tests.json",
        "passed": true,
        "count": 27,
        "error": null
      }
    ],
    "harnessErrors": []
  },
  {
    "suite": "10_WorkerWalking44",
    "files": [
      {
        "path": "Phase2A/PostGate/10_WorkerWalking44/Entrance_tests.json",
        "passed": true,
        "count": 44,
        "error": null
      }
    ],
    "harnessErrors": []
  }
]
```


## 23. Legacy informational

전/후 모두 raw-clock B-TROLLEY: Committed a pose every frame, LiveLoadPathPlayModeChecks.cs:118에서 같은 실패. A-SLEW2행 통과 후 중단하며 전체14 통과라고 보고하지 않는다. 기존 geometry/Worker_01 fixture 변경 없음. E-Worker obsolete geometry는 사전 coverage map의 과거 진단이며 이번 raw 실행에서는 미도달. 대체 gate에서 실제 의미를 검사했다.
```json
{
  "beforeError": "System.Exception: B-TROLLEY: Committed a pose every frame\r\n  at LiveLoadPathPlayModeChecks.Check (System.Boolean ok, System.String message) [0x0002d] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:69 \r\n  at LiveLoadPathPlayModeChecks.Tick () [0x005f5] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:118 ",
  "afterError": "System.Exception: B-TROLLEY: Committed a pose every frame\r\n  at LiveLoadPathPlayModeChecks.Check (System.Boolean ok, System.String message) [0x0002d] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:69 \r\n  at LiveLoadPathPlayModeChecks.Tick () [0x005f5] in D:\\UnityProject\\CraneSignalAI\\Assets\\Editor\\Prediction\\LiveLoadPathPlayModeChecks.cs:118 ",
  "cleanupVerified": true
}
```


## 24. A1–A31 결과

총33개(A16-1/A16-2 포함), A17–19는 선택 avatar OUTFITTING 상태의 원본Entrance44 실제 걷기/E/F로 검증. A29–31은 독립 audit/FPS/Console evidence로 합산했다. 최초 신규 test 자동화의 Canvas 등록 전 same-frame 클릭 실패는 WorkerChecksFirst에 보존했고, 렌더 frame 대기만 수정했다. 기존 test expected 변경은 없다. 최종 결과:
```json
[
  {
    "test": "A1",
    "passed": true,
    "data": null
  },
  {
    "test": "A2",
    "passed": true,
    "data": null
  },
  {
    "test": "A3",
    "passed": true,
    "data": {
      "languages": 3,
      "profiles": 4,
      "localizedPages": 12
    }
  },
  {
    "test": "A4",
    "passed": true,
    "data": {
      "workerId": "WKR_SIGNAL_01",
      "roleCode": "SIGNALER",
      "activeVisual": "ActiveWorkerVisual"
    }
  },
  {
    "test": "A5",
    "passed": true,
    "data": {
      "workerId": "WKR_RIG_01",
      "roleCode": "RIGGER",
      "activeVisual": "ActiveWorkerVisual"
    }
  },
  {
    "test": "A6",
    "passed": true,
    "data": {
      "workerId": "WKR_WELD_01",
      "roleCode": "WELDER_FITTER",
      "activeVisual": "ActiveWorkerVisual"
    }
  },
  {
    "test": "A7",
    "passed": true,
    "data": {
      "workerId": "WKR_OUTFIT_01",
      "roleCode": "OUTFITTING_PIPE",
      "activeVisual": "ActiveWorkerVisual"
    }
  },
  {
    "test": "A8",
    "passed": true,
    "data": {
      "characterCount": 1,
      "height": 1.8,
      "radius": 0.28,
      "center": [
        0.0,
        0.9,
        0.0
      ],
      "step": 0.25
    }
  },
  {
    "test": "A9",
    "passed": true,
    "data": {
      "eye": 1.7,
      "targetId": 56772,
      "cameraId": 61842,
      "interactionId": 55334
    }
  },
  {
    "test": "A10",
    "passed": true,
    "data": {
      "expected": [
        -51.0,
        -5.58793545e-08,
        -36.0771141
      ],
      "actual": [
        -51.0,
        -1.15484e-07,
        -36.0771141
      ],
      "errorM": 5.96046448e-08
    }
  },
  {
    "test": "A11",
    "passed": true,
    "data": [
      {
        "kind": "WALL",
        "requested": [
          -53.8799973,
          -1.15484e-07,
          -31.0
        ],
        "restored": [
          -54.07134,
          0.075,
          -31.46194
        ],
        "distanceXZ": 0.5000008,
        "reason": "NEAREST_WALKABLE_WITHIN_3M"
      },
      {
        "kind": "SEA",
        "requested": [
          110.0,
          0.0,
          110.0
        ],
        "restored": [
          -51.0,
          0.004999999,
          -35.0
        ],
        "distanceXZ": 216.670258,
        "reason": "FACILITY_VALIDATED_SPAWN"
      },
      {
        "kind": "DOOR_FRAME",
        "requested": [
          -54.0399971,
          0.04,
          -31.0
        ],
        "restored": [
          -54.23134,
          0.075,
          -31.46194
        ],
        "distanceXZ": 0.5000008,
        "reason": "NEAREST_WALKABLE_WITHIN_3M"
      }
    ]
  },
  {
    "test": "A12",
    "passed": true,
    "data": {
      "isolated": true,
      "pointCap": 2000,
      "retained": 1005,
      "decimationPasses": 3
    }
  },
  {
    "test": "A13",
    "passed": true,
    "data": null
  },
  {
    "test": "A14",
    "passed": true,
    "data": {
      "workers": 4,
      "labels": [
        "WKR_SIGNAL_01",
        "WKR_RIG_01",
        "WKR_WELD_01",
        "WKR_OUTFIT_01"
      ],
      "patterns": [
        "- - - -",
        "- . - .",
        "--------",
        ". . . ."
      ]
    }
  },
  {
    "test": "A15",
    "passed": true,
    "data": null
  },
  {
    "test": "A16",
    "passed": true,
    "data": {
      "plannerSupportsRolePreference": false,
      "newPathfinding": false,
      "signalCost": 2.0,
      "welderCost": 1.4
    }
  },
  {
    "test": "A16-1",
    "passed": true,
    "data": null
  },
  {
    "test": "A16-2",
    "passed": true,
    "data": null
  },
  {
    "test": "A17",
    "passed": true,
    "data": "Unmodified Entrance44 with active OUTFITTING_PIPE avatar; physical walking/E/F"
  },
  {
    "test": "A18",
    "passed": true,
    "data": "Unmodified Entrance44 with active OUTFITTING_PIPE avatar; physical walking/E/F"
  },
  {
    "test": "A19",
    "passed": true,
    "data": "Unmodified Entrance44 with active OUTFITTING_PIPE avatar; physical walking/E/F"
  },
  {
    "test": "A20",
    "passed": true,
    "data": {
      "voiceOnlyUnchanged": true,
      "playerUnchanged": true,
      "operatorUnchanged": true,
      "doorUnchanged": true
    }
  },
  {
    "test": "A21",
    "passed": true,
    "data": null
  },
  {
    "test": "A22",
    "passed": true,
    "data": {
      "cachedRenderers": true,
      "globalSceneSearches": 0
    }
  },
  {
    "test": "A23",
    "passed": true,
    "data": {
      "directory": "C:/Users/chlgu/AppData/LocalLow/DefaultCompany/CraneSignalAI\\WorkerRoutes\\20261021_150344_486",
      "count": 4
    }
  },
  {
    "test": "A24",
    "passed": true,
    "data": {
      "count": 4
    }
  },
  {
    "test": "A25",
    "passed": true,
    "data": null
  },
  {
    "test": "A26",
    "passed": true,
    "data": {
      "signalerCases": 35,
      "riggerCases": 65
    }
  },
  {
    "test": "A27",
    "passed": true,
    "data": null
  },
  {
    "test": "A28",
    "passed": true,
    "data": {
      "coverage": "visible Worker UI, all four profiles x KO/EN/VI, no auto-add"
    }
  },
  {
    "test": "A29",
    "passed": true,
    "data": "FontAudit.json; auto-saved font drift rolled back to exact allowed Worker glyphs"
  },
  {
    "test": "A30",
    "passed": true,
    "data": {
      "beforeFps": 12.881840855395833,
      "afterFps": 11.84947643935043,
      "dropPercent": 8.014106272807853
    }
  },
  {
    "test": "A31",
    "passed": true,
    "data": "First attempt found one lifecycle Runtime error; fixed + active Explore Play-stop reproduction + complete Gate rerun. Post-fix new Project Error/Warning=0; original error snapshot retained."
  }
]
```


## 25. FPS 전/후

F10 동일 player/camera/mode/screen1920×1080/quality0, 2.5초 warmup+10초 actual frame interval. 기능 비활성화 없음. 평균 12.882→11.849 FPS; 변화 -8.01%. 최소 7.822→10.157; 평균 frame 77.629→84.392ms. 20% 하락 아님. 단일10초 Editor sample의 자연 변동이며 성능 향상을 일반화하지 않는다. 원시 intervals 포함 JSON 제공.

## 26. Console baseline 대비

최초 대조(FirstFinal)에서 프로젝트 Runtime MissingReferenceException +1건을 발견했다. 앞선 Explore Play 종료 중 이미 파괴된 player를 OnDisable→Exit가 접근한 원인이다. 기존 Explorer 파일에 OnApplicationQuit/생존 여부 guard만 추가했다. 일반 Exit는 유지한다. LifecycleProbe.json은 Explore 활성 상태에서 Exit 호출 없이 Play를 종료한 재현이며 LifecycleAfter에서 같은 오류 없음. 이후 전체 Gate 재실행. 수정 전 로그를 삭제하지 않고 FirstFinal/Console.json, SaveSceneFontDrift/, PostGate_First/에 보존했다.

주의: 원래 Console 설정 복원 후 수정 코드 recompile 과정에서 기본 자동 clear 설정에 따라 live buffer가 reset됐다. 수동 Clear/filter 호출은 없다. 그러므로 시작→최종 단순 음수 delta를 전체 작업에 오류가 없었다는 근거로 쓰지 않는다. 시작/수정 전/수정 후 interval을 별도로 제공한다. 최종 PostFixBaselineConsole→Final interval의 신규 Project Runtime/Editor Error/Warning=0 여부를 판정한다. C# compile 경고/오류와 MCP 로그는 별도 분류한다.
```json
{
  "before": {
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
  },
  "after": {
    "errors": 6,
    "warnings": 136,
    "counts": {
      "MCP package": {
        "Error": 6,
        "Warning": 136,
        "Log": 0
      },
      "Project Runtime": {
        "Error": 0,
        "Warning": 0,
        "Log": 129
      },
      "Project Editor": {
        "Error": 0,
        "Warning": 0,
        "Log": 8
      }
    }
  },
  "delta": {
    "MCP package": {
      "Error": -12,
      "Warning": -24,
      "Log": 0
    },
    "Project Editor": {
      "Error": -1,
      "Warning": 0,
      "Log": -2
    },
    "Project Runtime": {
      "Error": 0,
      "Warning": 0,
      "Log": -64
    }
  },
  "newProjectProblems": [],
  "postFixBefore": {
    "errors": 3,
    "warnings": 11,
    "counts": {
      "MCP package": {
        "Error": 3,
        "Warning": 11,
        "Log": 0
      },
      "Project Runtime": {
        "Error": 0,
        "Warning": 0,
        "Log": 1
      }
    }
  },
  "postFixDelta": {
    "Project Editor": {
      "Error": 0,
      "Warning": 0,
      "Log": 8
    },
    "MCP package": {
      "Error": 3,
      "Warning": 125,
      "Log": 0
    },
    "Project Runtime": {
      "Error": 0,
      "Warning": 0,
      "Log": 128
    }
  },
  "postFixProblems": [],
  "historyNote": "FirstFinal captured +1 Project Runtime MissingReferenceException before correction. Shutdown guard fixed it; active Explore Play-stop reproduction and full final Gate follow. Console buffer reset during restored-default automatic recompile lifecycle; original snapshots and Editor.log evidence retained. No manual Clear or log filter used. Negative whole-task deltas are not evidence of absence of earlier errors."
}
```
Console flags 최종8066 복원. 최초 과거 sharing error1건, 작업 중 수정된 lifecycle error1건, MCP 로그를 혼합하지 않는다.

## 27. Main Scene saved / dirty

Main Scene 저장 완료, dirty=false, missing script=0. 최종 상태 전체:
```json
{
  "utc": "2026-10-21T15:16:21.8610737Z",
  "scene": "Assets/Scenes/Main.unity",
  "dirty": false,
  "playing": false,
  "compiling": false,
  "missingScripts": 0,
  "position": [
    -46.0,
    0.0,
    32.0
  ],
  "rotation": [
    0.0,
    0.0,
    0.0,
    1.0
  ],
  "scale": [
    1.0,
    1.0,
    1.0
  ],
  "parent": null,
  "controllerEnabled": true,
  "receiverEnabled": true,
  "faultComponentEnabled": false,
  "faultEnabled": false,
  "faultMode": "NONE",
  "captureDeltaTime": 0.0,
  "errors": 6,
  "warnings": 136,
  "logs": 137
}
```


## 28. Play Mode

종료: playing=false, compiling=false. 테스트용 captureDeltaTime=0 복원. 최종 Source compile error/warning0. package/native 경고는 Console 분류표를 기준으로 별도 보고한다.

## 29. LIVE root

position=(-46,0,32), rotation=(0,0,0,1), scale=(1,1,1), parent=null. 최초 snapshot과 같음. TowerCraneController/CommandReceiver 최종 enabled=true. Voice 단독 crane pose 불변 A20 및 기존 Prediction/Operator gate 확인.

## 30. Fault 최종

component.enabled=false / faultEnabled=false / faultMode=NONE. 최종 State.json 참조.

## 31. Phase 2A 최종 판정

CAUTION: 2A 기능·필수 gate PASS. 알려진 legacy raw-clock informational 실패와 누적 MCP lifecycle 로그가 남아 있다. 기존 expected/fixture를 바꿔 통과시키지 않았다. 작업 중 발견한 lifecycle 오류와 HMI 잘림은 각각 최소 guard/auto-size 조정으로 수정하고 재검증했다. Console 자동 reset으로 전체 작업 로그의 연속성이 끊긴 구간은 별도 snapshot으로 보존했다. 수정 후 검증 구간의 신규 프로젝트 오류/경고와 보호 상태 문제는 없다.

## 32. Phase 2B 시작 가능 여부

기술 gate 기준 진행 가능, 사용자 승인 전 Phase 2B 시작하지 않음. Worker stage reader/Load Select/Exclusion zone/role-aware near-miss/Session Risk는 생성하지 않았다. 이 보고 후 멈춘다. Git commit/push 없음.
