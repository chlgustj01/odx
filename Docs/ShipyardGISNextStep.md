# GIS next step — reserved design, not implemented

이번 단계는 신뢰도 라벨 정정이다. GIS 변환, 정합, 좌표 이동은 구현하지 않는다.

## 중앙 좌표 기준

새 `ShipyardGISCoordinateConverter`를 만들지 않는다. 기존 `ShipyardCoordinateConfig`를 확장해 아래 값을 한 곳에서 관리한다.

- originEasting
- originNorthing
- originElevation
- crsEpsg
- northRotationDeg (기본값 0)
- ProjectedToUnity(...), UnityToProjected(...)

현재 클래스의 `origin` / `northRotation`과 향후 필드의 마이그레이션은 다음 GIS 단계에서 설계한다. 서로 다른 회전 필드가 별도 기준으로 동작하게 하지 않는다. 이번 라벨 작업에서는 기존 클래스나 값에 변경을 가하지 않는다.

Projected north와 Unity +Z가 같은 기준이면 추가 회전을 넣지 않는다. 회전은 `ShipyardCoordinateConfig` 한 곳에서만 적용한다. Facility orientation에 northRotationDeg를 다시 더해 이중 회전하지 않는다. CRS·원점·높이 기준은 실제 입력 출처로 확인하며 임의 값을 만들지 않는다.

## 입력 파일 후보

- facilities.geojson
- buildings.geojson
- docks.geojson
- quays.geojson
- cranes.geojson
- roads.geojson
- rails.geojson
- coastline.geojson
- shipyard_origin.json

## F14 출처 간 불일치

Royal No.1–3의 기존 dimension은 SECONDARY로 유지한다. GIS/정사영상 footprint dimension과 다르면 자동 overwrite하지 않는다. 출처, CRS, 추출 방법, 날짜, 각 치수와 차이를 discrepancy report에 기록한다. OFFICIAL 기능/성능 문구는 SECONDARY 치수와 별도 항목으로 유지한다.

## LIVE rig 보호

현재 TowerCraneRoot world position은 (-46, 0, 32)다. GIS alignment preview에서 사용자 명시 승인 전까지 position / rotation / scale / parent를 변경하지 않는다. F01 GIS 위치에는 필요시 ghost 또는 preview marker만 생성한다. 기존 LIVE root나 Control Pivot을 GIS 배경에 reparent하지 않는다.
