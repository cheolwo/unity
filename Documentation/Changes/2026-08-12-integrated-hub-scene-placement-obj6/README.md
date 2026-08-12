# OBJ-6 Hub 입고 Gate Scene 배치

## 결과

- `SimulationWorldShell/LogisticsDistrict`의 기존 일반 물류센터 건물 표현을 `seedbed-object:town.hub-inbound-gate.a` wrapper prefab으로 교체했다.
- 차량, 출고 pallet·cargo와 기존 Cargo navigation은 유지했다.
- placement receipt는 `scene-placement:simulation-world-shell.logistics.hub-inbound-gate.a`다.
- Scene anchor는 `logistics.hub.inbound-gate`, DataBinding은 `HubReceiving:hub-receiving:sim.potato`다.
- wrapper의 `Entry`, `Exit`, `Vehicle`, `Cargo`, `Interaction`, `Label`, `CameraFocus` socket과 prefab 연결을 저장 Scene test에서 검증한다.

## 검증

- Unity 재컴파일: 오류 0건
- `통합전시관ScenePlacementTests`: 2/2 통과
- `SimulationWorldShellTests`: 10/10 통과
- Play Mode에서 Logistics District를 선택해 1600×900 Game View를 확인했다.
- Play Mode Console에는 OBJ-6과 무관한 로컬 턴마감 서버 미실행 `TurnClosingServerRequestFailed:0:ConnectionError` 1건이 발생했다.

## 화면

![Hub 입고 Gate가 배치된 물류 구역](integrated-hub-scene-placement-obj6.png)

이 화면은 업무를 실행하는 운영 화면이 아니라, 서버 권위의 Hub Receiving 계보에 연결된 모듈 Object가 대상 Scene에서 조화롭게 배치되는지 확인하는 Simulation Game View다.
