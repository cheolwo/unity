# Zone NPC Movement Sample

공유 World의 Zone별 NPC를 `NavMeshAgent`와 `Animator`에 연결하는 Presentation socket sample이다.

## 구성

- `NpcWaypointView`: `farm.field-a`, `logistics.loading-bay` 같은 semantic waypoint와 Transform 연결
- `ZoneNpcWaypointRegistry`: Zone 내부 waypoint key 중복과 배선 검증
- `NpcMovementView`: NPC stable ID, NavMeshAgent, Animator와 도착 행동 trigger 연결
- `ZoneNpcMovementController`: 서버 또는 simulation에서 받은 NPC snapshot을 stable ID 대상에 적용
- `WorldNpcMovementRouter`: 거점 간 이동과 창고 내부 이동 snapshot을 World Zone별 Controller로 전달
- `CargoWarehouseHandoffView`: 운송중·입고 Dock·보관 위치의 화물 VisualRoot와 운송자·입고작업자 NPC를 함께 갱신

`NpcMovementView`가 목적지에 도착해도 서버 Command를 호출하거나 canonical 업무를 완료하지 않는다. 도착 시 걷기 속도를 0으로 만들고 Inspector에 연결된 animation trigger만 실행한다. 상차, 검수, 피킹, 배송 완료는 별도 interaction 확인과 서버 Command 성공 후 canonical snapshot을 다시 조회해야 한다.

운영 NPC snapshot은 `CanonicalTaskStableId`가 필수다. simulation NPC는 이 필드를 가질 수 없으며 UI에서 `SimulatedFixture`로 구분해야 한다.

화물 인계는 `InTransit → ArrivedAtWarehouse → ReceivingCompleted` 순서로 보이지만 View가 다음 상태를 스스로 만들지는 않는다. 기존 기사 하차 Command와 창고 입고 Command가 성공한 뒤 `CargoWarehouseHandoffQueryUseCase`로 canonical snapshot을 다시 조회해 `CargoWarehouseHandoffApplicator`에 전달한다.
