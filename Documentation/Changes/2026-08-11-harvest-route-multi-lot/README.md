# HarvestLot 다중 원장 선택

Unity가 Simulation session의 여러 수확물 allocation과 Task를 Lot별로 보존하고, 화면 object와 명시적으로 연결된 HarvestLot만 생산자 카드에 표시한다.

- object stable ID와 HarvestLot stable ID를 일대일 mapping으로 관리한다.
- 여러 allocation을 `SingleOrDefault`로 축약하지 않는다.
- 선택한 Lot의 allocation·Task·Effect·남은 Tick만 현재 카드 상태로 투영한다.
- mapping 대상 결과가 없거나 중복 mapping이면 기존 선택을 유지하고 차단한다.
- 화면 object ID에서 원장 Lot ID를 추측하지 않는다.
- 실제 판매·배송·수출·정산을 실행하지 않는다.

Play Mode 카드 상단에는 `화면 potato-001 → 원장 potato.20260407.r1` 연결이 표시된다. 다중 Lot 격리는 EditMode에서 Applied 첫 번째 Lot과 InProgress 두 번째 Lot을 전환하며 검증했다.

![화면 object와 HarvestLot 원장의 명시적 연결](harvest-route-multi-lot-selection.png)
