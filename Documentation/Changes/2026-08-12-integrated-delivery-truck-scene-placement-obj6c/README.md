# OBJ-6C 화물 배송 차량 Scene 배치

## 결과

- `SimulationWorldShell/LogisticsDistrict`의 기존 일반 Van 표현을 `seedbed-object:town.delivery-truck.a` wrapper prefab으로 교체했다.
- placement receipt는 `scene-placement:simulation-world-shell.logistics.delivery-truck.a`다.
- Scene anchor는 `logistics.cargo-journey.delivery-truck`, DataBinding은 `CargoJourney:cargo-journey:sim.potato.farm-hub`다.
- 기존 `cargo:sim.potato-1` Object Focus와 물류 이동 Preview·Confirm·Tick 경계는 유지했다.
- 차량 wrapper는 Driver·Cargo·RouteEntry·RouteExit·Interaction·Label·CameraFocus socket을 제공하지만 배차나 Cargo 상태를 소유하지 않는다.
- 공용 pallet과 농장 출하 crate는 이번 배치에서 제외해 O5 상태를 유지한다.

## 검증

- Unity 재컴파일: 오류 0건
- `통합전시관ScenePlacementTests`: 3/3 통과
- `SimulationWorldShellTests`: 10/10 통과
- Cargo Object Focus에서 1600×900 Game View를 확인했다.
- Play Mode Console에는 OBJ-6C와 무관한 로컬 턴마감 서버 미실행 `TurnClosingServerRequestFailed:0:ConnectionError` 1건이 발생했다.

## 화면

![Cargo Object Focus에서 확인한 화물 배송 차량](integrated-delivery-truck-scene-placement-obj6c.png)

이 화면은 배송을 자동 확정하는 화면이 아니다. 서버 권위의 Cargo Journey를 읽고 명시적 Preview·Confirm 경계를 유지하는 Simulation 장면에 모듈 차량을 배치한 결과다.
