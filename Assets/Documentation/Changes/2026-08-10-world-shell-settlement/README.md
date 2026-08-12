# WORLD-SHELL-0·SETTLEMENT-SCENE-0

## 구현 결과

- 기존 공공데이터 `WorldBootstrapScene`과 분리된 `SimulationWorldShell` Scene을 추가했다.
- 하나의 읽기 전용 `SimulationWorldShellSnapshot`을 `WorldMapRoot`와 `SettlementInteriorRoot`가 공유한다.
- root 전환은 `SimulationWorldShellStateMachine`의 관찰 규모와 stable-ID 선택만 바꾸며 Command·Tick·재고 변경 port를 갖지 않는다.
- HUD는 명시적인 `SimulationFixture`의 GameDate, Tick, Revision, Treasury, Labor, Market Food, Reserve Food, FoodSecurityDays와 Active Tasks를 표시한다.
- Pause와 Speed는 후속 권위 연결 전까지 `미연결` 비활성 상태다.
- 첫 정착지에 Farm, Town, Market, Storage, Logistics, Residential District와 기능 없는 Garrison·Gate placeholder를 배치했다.
- District는 `SimulationWorldDistrictView` Presentation socket이며 새 Simulation Entity나 manager가 아니다.

## 시각 증거

### World Map

![World Map](world-map.png)

### Settlement Interior

![Settlement Interior](settlement-interior.png)

두 Play Mode Game View 모두 `Year 1 · 04-12`, `Tick 12`, `Revision 12`, 같은 session stable ID를 표시한다.

## 검증

- Unity 재컴파일: 오류 0건
- `SimulationWorldShellTests`: 5/5 통과
- `DioramaCameraTests`: 4/4 통과
- `Ssalddel.Unity.Tests.EditMode` 전체: 44/44 통과
- 최종 Play Mode World Map·Settlement Interior 전환: Console 오류 0건

## 경계

- 서버 HTTP adapter, Preview·Confirm, 실제 WorldTick 입력과 경제 원장 변경은 구현하지 않았다.
- 기존 공공데이터 `WorldBootstrapScene`과 `ProjectSettings/EditorBuildSettings.asset`은 변경하지 않았다.
- Synty prefab을 Domain이나 Simulation authority로 사용하지 않았다.
- 다음 서버 권위 Gate는 `SETTLEMENT-ECONOMY-1`이다.
