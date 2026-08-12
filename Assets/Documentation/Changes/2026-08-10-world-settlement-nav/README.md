# WORLD-SETTLEMENT-NAV-0

## 결과

- 하나의 `SimulationWorldShellSnapshot` 위에서 World Map → Settlement → District → Object를 이동한다.
- `Back`은 Object → District → Settlement → World Map 순서로 이동한다.
- 상위 선택은 보존하고 하위 선택만 해제한다.
- World/Zone/Object 카메라 focus는 Presentation만 변경하며 Tick 12·Revision 12는 유지된다.
- Settlement marker 1개, District 8개, 감자 HarvestLot 1개를 명시적 navigation target으로 연결했다.

## 시각 증거

- `farm-district.png`: Farm District Zone focus와 breadcrumb
- `harvest-lot-object.png`: `harvest-lot:potato-001` Object focus와 선택 강조

## 경계

- 이 Scene은 Simulation fixture를 읽기 전용으로 표시한다.
- 클릭과 카메라 이동은 Decision, Task, Tick, 재고를 변경하지 않는다.
- 감자 상자 Renderer 수는 300kg의 권위가 아니다.
- Pause와 Speed는 계속 미연결 상태다.

## 검증

- `SimulationWorldShellTests` 8/8 통과
- `DioramaCameraTests` 4/4 통과
- Play Mode에서 District → Object → Back 3회 후 Tick 12·Revision 12 유지
- Play Mode Console 오류 0건
