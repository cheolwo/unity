# 시야 기반 동적 월드 수직 단위

`SimulationWorldShell` 하나에서 플레이어 주변 L2 타일 창, 자료 준비 상태, 카메라 시야와 건물 표현 승격을 함께 검증했다.

![1인칭 시야 기반 동적 월드](visibility-tree-first-person-game-view.png)

## 화면에서 확인한 내용

- 플레이어 중심 `3×3` 활성 타일과 `5×5` 준비·추적 타일
- 실제 DEM·토지피복·배치 마스크가 없음을 숨기지 않는 `자료 대기 25`
- 1인칭 카메라 절두체 안의 실제 시야와 이동 예측·화면 여백 수
- 결정적 Scenario 건물의 충돌 없는 프록시와 의미 기반 Synty 상세 승격
- 화면 밖 표현의 비활성 캐시와 재사용 상태
- 이동 안전 Gate, `WorldTick 0 / 활동 Revision 0`과 `PresentationOnly` 경계

## 권위와 한계

화면의 Barn·Silo·Farmhouse·Greenhouse·ProduceStand는 첫 동적 생성 흐름을 확인하는 `Scenario` 자료다. 실제 관측 건물이나 운영 시설을 뜻하지 않으며, Unity 이동·카메라·Prefab 생성은 업무 완료와 서버 상태를 변경하지 않는다.

현재 실제 공간 산출물은 아직 `WaitingForSpatialArtifact`다. Fixture는 기존 `ScenarioTerrainPreview`의 Collider를 이동 검증에만 사용하며 이를 DEM 기반 물리 지형 완료로 간주하지 않는다.

## 검증

- Simulation 서버 집중 테스트: 5/5 통과
- Unity EditMode 집중 테스트: 7/7 통과
- 저장된 `SimulationWorldShell` PlayMode 집중 테스트: 1/1 통과
- Unity Pipeline 실제 Play Mode Game View: 2026-08-14 캡처
- 별도 통합 Scene의 Simulation 서버 연결 오류는 로컬 서버가 없는 상태의 기존 경계이며, 이번 fixture 스트리밍 검증에서는 타일·시야 상태와 `WorldTick` 불변성을 직접 단언했다.
