# OBJ-6D-2 농장 출하 PalletCrate Scene 배치

- Scene: `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`
- 배치 영수증: `scene-placement:simulation-world-shell.farm.pallet-crate.a`
- 모판 Object: `seedbed-object:farm.pallet-crate.a`
- 구역/Anchor: `district:farm` / `farm.outbound.pallet-crate`
- 결속: `CanonicalProductHarvestCargo:cargo:sim.potato.20260407.r3`
- 위치/회전: `(6.3, 0.2, -1.2)` / `(0, 18, 0)`

## Runtime 증거

`integrated-farm-pallet-crate-scene-placement-obj6d2.png`은 2026-08-12에
`SimulationWorldShell` Play Mode에서 `Preview Farm District`와
`Preview Harvest Lot`을 실행한 뒤 1600x900 Game View로 캡처했다.

화면 중앙의 감자 수확 상자는 수확 Lot의 적재물이고, 우측 상단의 빈 PalletCrate는
농장 출하 대기 모듈이다. 두 Object는 서로 다른 stable ID와 배치 영수증을 가지며,
이 배치는 실제 상차·운송 확정을 뜻하지 않는다.

콘솔에는 로컬 서버를 실행하지 않은 상태에서 발생한 기존 기준선
`TurnClosingServerRequestFailed:0:ConnectionError` 1건이 있었고, Scene 배치 검증과는 분리한다.
