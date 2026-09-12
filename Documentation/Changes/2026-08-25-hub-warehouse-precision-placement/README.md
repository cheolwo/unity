# Hub 창고 정밀 배치

## 변경 결과

- canonical `SimulationWorldShell`의 `LogisticsDistrict` 아래에 25×20m Hub 창고를 저장했다.
- 작업 구역 5개와 통로 2개, Synty 소품 7종, NPC 업무 경로 3개를 `placement-control-hierarchy.v2` 배치 검증 기록으로 결속했다.
- 원 Synty Prefab은 지역 변환을 바꾸지 않고 wrapper에서 위치·회전·균일 크기와 충돌체를 통제한다.
- 검수·적재·피킹·포장·출고 준비 경로는 읽기 전용 표현이며 Simulation 상태를 직접 변경하지 않는다.

## 검증 수준

- Unity 6000.5.6f1 EditMode `Hub창고정밀배치Tests` 4/4 통과.
- Scene 저장 구조, 계획 hash, 소품 경로, 배치 검증 기록, NPC 경로 기준점을 검사했다.
- Play Mode 실제 이동, NavMesh 통행, Game View와 Console은 검증하지 않았다.
- 따라서 이 기록은 코드·시험·저장 Scene 구조 증거이며 실제 플레이 화면 완료 증거가 아니다.
