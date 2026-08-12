# OBJ-6D-1 공용 화물 Pallet Scene 배치

## 결과

- `SimulationWorldShell/LogisticsDistrict`의 기존 outbound pallet 표현을 `seedbed-object:shared.cargo-pallet.a` wrapper prefab으로 교체했다.
- 기존 Cargo box, 화물 배송 차량, Hub 입고 Gate와 Cargo navigation은 유지했다.
- placement receipt는 `scene-placement:simulation-world-shell.logistics.cargo-pallet.a`다.
- Scene anchor는 `logistics.warehouse-handoff.cargo-pallet`이다.
- DataBinding은 `WarehouseHandoff:cargo-handoff:sim.potato.20260407.r3.inbound-91`이다.
- wrapper는 Cargo·Forklift·Interaction·Label·CameraFocus socket을 제공하지만 입고·재고 상태를 소유하지 않는다.
- 농장 출하 Pallet Crate는 이번 배치에서 제외해 O5 상태를 유지한다.

## 검증

- Unity 재컴파일: 오류 0건
- `통합전시관ScenePlacementTests`: 4/4 통과
- `SimulationWorldShellTests`: 10/10 통과
- Logistics District에서 1600×900 Game View를 확인했다.
- Play Mode Console에는 OBJ-6D-1과 무관한 로컬 턴마감 서버 미실행 `TurnClosingServerRequestFailed:0:ConnectionError` 1건이 발생했다.

## 화면

![Warehouse Handoff 위치에 배치한 공용 화물 Pallet](integrated-cargo-pallet-scene-placement-obj6d1.png)

이 화면은 pallet 위 Cargo의 입고나 재고를 자동 확정하지 않는다. 서버 권위의 Warehouse Handoff 계보를 읽는 Simulation 배치 결과다.
