# 통합 Object 모판 OBJ-2~3

## 결과

- 전시관을 완성 장면 모음이 아니라 Scene에 개별 배치할 Object의 검증 모판으로 분리했다.
- 감자 수확 상자, Hub 입고 Gate, 음식 픽업 인계 상자에 wrapper prefab과 `SeedbedObjectRoot`를 부여했다.
- Visual Catalog가 semantic visual/placement key를 실제 Unity prefab, footprint, bounds, required socket과 연결한다.
- `통합Object모판`은 중앙 Preview bay에 Object 하나만 표시하고 선택 변경과 Play Mode 회전을 제공한다.
- Scene 업무 상태, 주문, 재고, 배차와 운영 Command는 Object나 Catalog가 소유하지 않는다.

## 검증

- Unity batch `BuildPreviewScene`: 종료 코드 0
- 전용 EditMode: 3/3 통과
- Play Mode Console 오류: 0건
- Game View: [integrated-object-seedbed-obj3.png](integrated-object-seedbed-obj3.png)

## Gate 경계

- O5 `RuntimeVerified`: 세 Object 모두 독립 Preview와 socket/bounds 검증 완료
- O6 `PromotedToScene`: 미완료
- 대상 업무 Scene의 `ScenePlacement`와 승격 receipt가 생기기 전에는 O6로 해석하지 않는다.
