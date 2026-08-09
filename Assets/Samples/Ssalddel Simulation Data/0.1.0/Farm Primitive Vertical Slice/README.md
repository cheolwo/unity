# Farm Primitive Vertical Slice

생산자 소유권으로 필터된 operational 농장 projection을 `FarmTileView`, `CropView`,
`SensorView`에 stable ID로 적용하는 primitive sample이다.

- 공개 작물 기준 ID/출처와 실제 재배 생육 상태를 분리한다.
- 센서 원시값은 Unity에서 재판정하지 않고 서버의 상태·규칙 revision·근거 card ID를 표현한다.
- 생산자 NPC는 canonical 농장작업의 semantic waypoint를 NavMeshAgent에 적용하며 도착으로 서버 작업을 완료하지 않는다.
- Simulation fixture는 `SourceTypeCode=SimulatedFixture`로 명시하며 운영 API 실패를 대신하지 않는다.
- 위치, 주소, 연락처와 소유자 사용자 ID는 Unity 계약에 포함하지 않는다.

## 6x6 토양 타일 Simulation

기존 operational `FarmPlot` Projection은 그대로 유지하고, 감자 첫 playable을 위한 별도 `FarmSoilTileSimulationDataSnapshot`을 표시한다.

- 36개 타일은 stable ID와 grid 좌표를 가지며 중복 좌표·누락 타일을 거부한다.
- 토양 profile·수분 관측 상태와 경작 상태를 분리한다.
- `Untilled`, `Tilled`, `Sown`은 Simulation 상태이며 실제 농지 상태가 아니다.
- 타일을 선택하면 Projector가 결정한 토양·수분·경작·작업 상태와 밭갈이 Preview 가능 여부를 표시한다.
- 타일 View는 color token만 적용하고 상태를 추론하지 않는다.
- 토양, 고랑, 씨앗과 작물 외형은 각 타일 `VisualRoot` 교체 단계에서 확장한다.

## FARM-2 밭갈이 폐루프

선택한 `Untilled` 타일은 다음 단계를 각각 명시적으로 거친다.

`선택 → Preview → Confirm → Simulation Tick → 새 Snapshot → View Reconcile → Tilled`

- Preview와 Confirm은 snapshot을 변경하지 않는다.
- Confirm된 command는 snapshot·scenario·rule·revision·tile stable ID를 고정한다.
- Tick만 새 revision의 snapshot을 반환하며 원본 snapshot을 수정하지 않는다.
- 타일 클릭, NPC 도착, animation/FX 완료는 Confirm이나 Tick을 자동 실행하지 않는다.
- operational API 실패를 이 Simulation fixture로 대체하지 않는다.

농부 이동·정지·회전·작업 animation은 후속 FARM-3 범위다.
