# WORLD-STREAM-1 1인칭 동적 공간 타일

`SimulationWorldShell` 하나에서 대관령 Farm 플레이어 위치를 기준으로 EPSG:5186 L2 500m 타일의 런타임 생명주기를 검증했다.

![1인칭 동적 공간 타일 Game View](dynamic-tile-streaming-first-person-game-view.png)

## 화면에서 확인한 내용

- 1인칭 WASD 경관과 기존 Synty Farm 시각 자산을 유지했다.
- 현재 중심 타일 주변 3×3은 활성, 5×5는 준비 대상으로 상태판에 표시된다.
- 청록 경계는 활성 창, 주황 경계는 준비 창이다.
- 타일 Root는 창을 벗어나면 풀로 반환되어 25개 Slot을 재사용한다.
- 상태판은 데이터 원본을 `Fixture`로 표시하고 실제 DEM·토지피복·배치 마스크가 `자료 대기`임을 알린다.
- 동적 경계와 카메라 이동은 `PresentationOnly`이며 `WorldTick`과 상태 개정을 바꾸지 않는다.

## 증거 범위와 제한

이 PNG는 Unity Editor의 실제 Play Mode Game View다. 다만 화면의 지형은 기존 `ScenarioTerrainPreview`이며 실제 DEM 기반 Terrain이 아니다. Fixture는 공간 산출물 주소·hash·높이를 만들지 않고 Collider 없는 경계만 생성한다. 실제 산출물 다운로드, SHA-256 검증, Halo 조립과 Terrain 활성화는 다음 단계다.

검증 결과는 서버 집중 4/4, Unity EditMode 4/4, `SimulationWorldShell` PlayMode 1/1 통과다. 실제 Simulation 서버를 실행한 Unity HTTP 통신, 운영 API·운영 DB·배포는 이 화면의 증거 범위가 아니다.
