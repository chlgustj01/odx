# Shipyard value provenance / SECONDARY

이 codebook은 프로젝트의 값 단위 출처 상태다. 기존 Excel의 confidence A/B/C 등급과 별도이며, 기존 schema나 Accident Evidence의 등급 체계를 교체하지 않는다.

`Assets/StreamingAssets/ShipyardDB/value_status_codebook.json`에서 OFFICIAL, SECONDARY, ESTIMATED, DEMO, UNKNOWN의 정의를 관리한다. SECONDARY는 **2차 자료에서 확인되었으나 공식/1차 출처에서는 직접 확인되지 않은 값**이다. OFFICIAL로 승격하거나 INFERRED/C/UNKNOWN으로 치환하지 않는다. Runtime 필드는 string이고 알 수 없는 미래 코드는 원문으로 표시한다.

## F14의 값 단위 분리

- facility_master: function_value_status=OFFICIAL, function_capabilities는 확인된 두 문구만 포함하며 function_source는 공식 시설 페이지 두 언어 출처만 포함한다.
- facility_master: dimension_value_status=SECONDARY, dimension_status_note에 공식 시설 페이지에서 치수 직접 확인되지 않음을 표시한다.
- floating_dock_master: Royal No.1–3의 value_status=SECONDARY, official_dimension_confirmed=false. 원래 제원 숫자, source, confidence B는 그대로 보존한다.

공식 기능 문구:

1. Suezmax-class launching capability
2. 78,000-ton-class LNG carrier re-floating / repair capability

이는 사용자가 확인한 기존 F14 공식 근거 범위다. 추가 성능·기수·크기를 공식 정보로 만들어내지 않는다.

## Runtime 흐름

JSON → ShipyardPublicDatabase의 문자열 필드 → ShipyardFacilityAdapter → ShipyardEvidenceLabels → DB DETAILS / Royal PublicAssetMetadata / Explorer E Detail.

ShipyardDatabasePanel의 F14 요약은 FUNCTION: OFFICIAL / DIMENSION: SECONDARY다. 전체 F14의 모든 정보를 일괄 SECONDARY로 분류하지 않는다. A/B/C는 원본 Workbook source grade임을 따로 표시한다. 새로운 status를 이유로 시설을 필터링하지 않는다.

전처리 정책은 Editor/ShipyardDB/shipyard_value_status.py에서 재적용하므로 원본 Excel을 다시 정규화할 때 라벨이 되돌아가지 않는다. 원본 Excel을 수정하지 않는다. 향후 MasterDB 연결 시 같은 codebook 정의를 사용하고 원본 등급과 값 상태를 혼합하지 않는다.

GIS 예약 원칙은 ShipyardGISNextStep.md에 있다. 이번 단계는 GIS나 자동 제어를 구현하지 않는다.

## 2026-10-21 facility evidence correction

Default XLSX source moved to `C:\Users\chlgu\OneDrive\바탕 화면\한오엔 최종프로젝트\DB\한화오션_거제사업장_시설DB (1).xlsx`. SHA-256 remains `60bf40ec0719fccb58e583831d6a21deb538c6d63615f36c07e65d3461adbd3b`. `preprocess_shipyard.py` now accepts this as its default source; a positional override is retained. Workbook parsing and cell mappings are unchanged. Read-only cell re-extraction ignores the known embedded image15 CRC issue.

`value_evidence` adds value-level labels/source/verified_date without replacing original confidence grades. F14's existing fields and Royal dimensions/statuses are unchanged.

- F01/F02: only dock L/W/D is OFFICIAL, verified 2026-10-20.
- F09: total quay length 3600m and water depth 11–14m are OFFICIAL. The 600/1700/1300m breakdown is DEMO reconstruction, to be replaced with actual quay geometry in the GIS phase.
- F15: area 45,000㎡ is OFFICIAL; the square footprint shape is ESTIMATED / RECONSTRUCTED.
- F19: the retained 210×54m model is ESTIMATED. The official source states a maximum single-factory dimension but does not specifically identify F19. No measured/official F19 dimension is claimed.

For these user-confirmed official values, source is https://www.hanwhaocean.com/en/whoweare/ig/facilities/ and verified_date is 2026-10-20. This does not elevate other facility values to OFFICIAL. The official verification date is user-specified and is separate from the current execution date. Models, transforms, hierarchy, LIVE rig and safety logic are unchanged.
