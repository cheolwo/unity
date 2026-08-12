# OBJ-5 첫 대상 Scene 이식

## 결과

- `SimulationWorldShell/FarmDistrict`의 기존 감자 상자 vendor 표현을 `seedbed-object:farm.potato-harvest-box.a` wrapper prefab으로 교체했다.
- `harvest-lot:potato-001` navigation과 Object Focus camera anchor는 그대로 유지했다.
- `통합전시관ScenePlacementView`가 placement stable ID, Scene·Zone, profile revision, anchor, DataBinding과 wrapper root를 검증한다.
- 저장 Scene에는 `scene-placement:simulation-world-shell.farm.potato-harvest-box.a`가 정확히 하나 존재한다.
- 나머지 여섯 Object는 O5에 남아 있으며 자동 배치하지 않았다.

## 검증

- Unity 6000.5.6f1 재컴파일: 오류 0건
- 전용 Scene placement EditMode: 1/1 통과
- 기존 `SimulationWorldShellTests`: 10/10 통과
- 서버 통합전시관 집중: 17/17 통과
- portable Unity mapper 집중: 17/17 통과
- Object Focus Game View: `integrated-object-scene-placement-obj5.png` 1600×900

## 확인된 기준선 오류

- Play Mode에서 로컬 턴마감 서버가 실행되지 않아 `TurnClosingServerRequestFailed:0:ConnectionError` 1건이 발생했다.
- 오류 stack은 `턴마감ServerAuthorityRepository`이며 이번 Scene placement·prefab·navigation 검증과는 무관하다.
- 오류를 fixture 성공으로 숨기거나 운영 연결 성공으로 기록하지 않았다.

## 경계

- 감자 수확 상자만 O6 `PromotedToScene`이다.
- Scene 배치는 Presentation이며 HarvestLot 상태나 수량의 권위를 소유하지 않는다.
- 운영 API/provider 호출, 실제 저장, 주문·배차·관수 Command를 수행하지 않았다.
