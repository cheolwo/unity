# LOGISTICS-MOVEMENT-1

감자 Cargo 300kg을 선택해 route Preview, 명시적 Confirm, WorldTick 3회를 거쳐 도착 후보까지 표시한다.

- authority: `SimulationFixtureAuthority` test double
- production seam: Simulation Server 물류 Preview·Confirm·Tick API
- invariant: Cargo stable ID, HarvestLot·PackageLot lineage, 300kg reservation
- boundary: 차량 animation은 Presentation이며 도착 후 검수 전에는 destination 재고가 아니다.

대표 화면: `arrival.png`
