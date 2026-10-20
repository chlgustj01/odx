# CraneSignalAI / Shipyard Safety Digital Twin

조선소 시설, 크레인 작업, 작업자 동선과 과거 사고 근거를 연결한 Unity 시연 프로젝트입니다.

## 실행

1. Unity Hub에서 이 저장소를 프로젝트로 추가합니다.
2. **Unity 6000.3.26f1**로 열고 Packages 복원과 Asset import를 기다립니다.
3. `Assets/Scenes/Main.unity`를 열고 Play를 누릅니다.

Unity URP 17.3.0, Input System, UGUI/TextMeshPro를 사용합니다. Unity MCP 패키지는 `Packages/manifest.json`의 OpenUPM registry를 통해 복원합니다. Git에는 `Assets`, `Packages`, `ProjectSettings`와 문서가 포함되며 `Library`, `Temp`, `Logs` 등은 자동 재생성됩니다.

## 주요 화면

- **SHIPYARD:** 22개 시설 선택·검색, OVERVIEW → FACILITY → ASSET, 부드러운 카메라 이동.
- **LIVE:** 현재 CraneState, 명령 예상 경로·Swept Area, 실제·잔여 이동 추적, 작업자 위험, Verification, STOP 검증과 Warning Lead Time.
- **WORKER ROUTE:** 직종별 직행·우회 동선, 상대 위험·노출, DEMO 보고와 작업 전 브리핑.
- **EXPLORE:** 시설 화면의 EXPLORE FACILITY에서 1·3인칭 현장 탐색, E 상세 조회와 별도 위험 HUD.
- **PLAN:** 작업조건 입력 → 802건의 정규화 사고 DB에서 설명 가능한 유사사례 Top 3/5/10 검색. 근거등급, 결측, 비교 이유, 원문과 출처 제공.

## 기존 타워크레인 조작

F01 선택 → ENTER FACILITY → **TC-LIVE**를 선택합니다. Game 뷰에 포커스를 둔 상태에서 조작합니다.

| 입력 | 동작 |
|---|---|
| A / D | 좌·우 선회 |
| ↑ / ↓ | 트롤리 바깥·안쪽 |
| W / S | 후크 상승·하강 |
| Space | 운전자 STOP |

음성 명령은 구조화된 JSON을 TCP `127.0.0.1:5000`으로 받습니다. 명령 수신 자체는 크레인을 움직이지 않으며, 마이크/STT는 아직 연결되지 않았습니다.

EXPLORE 중에는 WASD 이동, Shift 달리기, 마우스 시선, V 시점 전환, E 상세, Tab 커서, ESC 복귀를 사용합니다. 탐색 또는 PLAN 중에는 크레인 키 입력을 차단하고 종료 후 복구합니다.

## 화물 설정

`TowerCraneRoot/SlewPivot/JibRoot/TrolleyCarriage/HookBlock/LoadRoot`의 `LoadProfile`에서 이름, 길이·폭·높이, 무게, 방향을 변경합니다. 영구 저장은 Edit Mode에서 변경 후 Scene을 저장합니다. 화물 변경 후 새 유효 명령으로 예측을 다시 생성합니다. 무게는 현재 표시 정보이며 동역학·과하중 판정에 연결되지 않았습니다.

## 사고 근거 데이터

- Runtime JSON: `Assets/StreamingAssets/AccidentDB/`
- 전처리 도구: `Assets/Editor/AccidentEvidence/preprocess_accidents.py`
- Python 3 + `openpyxl` 필요. 원본 XLSX는 저장소에 포함되지 않습니다.

```text
python Assets/Editor/AccidentEvidence/preprocess_accidents.py "source.xlsx" --out Assets/StreamingAssets/AccidentDB --reports "reports"
```

유사도는 사고확률이 아닙니다. UNKNOWN은 불일치로 처리하지 않고 비교 가능한 가중치로 정규화합니다. 근거등급은 유사도와 분리합니다. 원본은 미검수 사례를 포함하며 CSI 해상·선박 자료를 모두 실제 조선소 사고로 해석하지 않습니다.

## 구현 범위

현재는 Unity Editor에서 검증한 교육·발표용 프로토타입입니다. 조선소 외형·배치는 공개 자료를 참고한 재구성이며 실측 CAD 복제가 아닙니다. 실제 장비 PLC, CCTV/GPS/UWB, 영구 보고 DB, LLM/Vector DB는 연결하지 않았습니다. PLAN은 사고 근거 검색용이며 WorkPermit·작업간섭 엔진·자동 승인은 구현하지 않았습니다. Fault Injection은 기본 비활성입니다.

## 검증 기록

최신 기능 통합 시 검색 Core 12개, PLAN UI 13개, 기존 화면·크레인 회귀 19개, Explore 17개, WorkerRoute 11개가 Unity Editor에서 통과했습니다. C# 컴파일 오류·경고 0개를 확인했습니다. 배포 실행 파일·현장 실장비 검증은 별도입니다.

에디터 검증 도구는 `Assets/Editor/`에 있습니다. 과거 setup 도구 일부는 최초 개발 환경의 경로를 포함하므로 일반 실행에는 저장된 Main Scene을 사용하고, setup 재실행 전 경로를 확인하세요.
