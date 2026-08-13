# 평창군 공간 Tile·Area·AreaSet 생성 Pipeline

## 적용 결과

- EPSG:5186 고정 격자 L0 8km, L1 2km, L2 500m 계약과 Halo·세계 좌표 seed를 추가했다.
- 대관령면 Farm, 진부면 Hub, 평창읍 Town을 Area로 분리하고 Farm→Hub→Town 두 `ScenarioRoute`를 하나의 AreaSet으로 묶었다.
- 환경부 면적 배분과 Synty 경관 개체 계획을 별도 산출물로 분리했다. 세분류 위치는 `StatisticallyAllocated`이고 실제 관측 위치가 아니다.
- 대관령면 기존 수작업 경관을 L2 500m `Reference Tile`로 지정했다.
- 시각 자산 대장에 footprint·여백·경사·collision·LOD·군집·회전·Triangle·Material Slot·Draw Call·Shadow Caster·Collider·Animator 비용을 연결했다.
- 카메라 거리에 따라 L0/L1/L2 표현 하나만 활성화하는 `PresentationOnly` Loader를 추가했다.
- 8개 중간 검증 뒤 `final-visual-asset-binding`을 Pipeline의 마지막 단계로 추가했다. 공간·경관 계획은 의미 기반 `VisualKey`까지만 만들고, 마지막 단계가 토지피복·영역 역할·원본 경사 조건을 통과한 키를 현재 Synty 구성 대장의 Prefab으로 연결한다.
- `SimulationWorldShell` 재생성 결과 `legal-dong-scenic-catalog.v1`에서 43건을 연결하고 0건을 거부했다. 연결 결과는 표현 전용이며 공간·건물·Simulation 고유 식별자를 바꾸지 않는다.

## 공간자료 경계

- 기본 표고: Copernicus DEM GLO-30 30m, EPSG:5186, NoData `-32767`, 높이 단위 m, EGM2008 geoid.
- 국내 비교 표고: VWorld·국토지리정보원 DEM 90m, NoData `-9999`. 수직 기준은 확보한 원본 metadata에서 확인되지 않아 미확정으로 기록했다.
- 위치 후보: ESA WorldCover 2021 v200 10m, EPSG:5186, NoData `0`.
- 전체 구성 목표: 환경부 2024 평창군 세분류 토지피복 면적 통계.
- 하천·호소는 `9.2042㎢`, 기타 나지는 별도 `23.6943㎢`다.

WorldCover 후보가 환경부 목표보다 적은 농업·수계·기타 나지는 공간을 꾸며내지 않고 `UnresolvedTargetArea`로 남겼다. 감자밭은 환경부 통계가 아니라 대관령 Farm `SimulationScenario` 표현이다.

실제 DEM은 출처 대장과 `PhysicalElevation` 계약에 연결했지만, 현재 Scene 연속 지형 Mesh는 기존 `ScenarioTerrainPreview`다. 따라서 이 화면은 DEM 정점·경사·수계 실제 배치 증거가 아니다.

## 표현 증거

1. `01-world-overview.png`: Farm→Hub→Town AreaSet 전체 배치.
2. `02-daegwallyeong-farm-reference-tile.png`: 대관령면 수작업 Reference Tile.
3. `03-farm-hub-corridor.png`: Farm→Hub 전환 회랑.
4. `04-jinbu-hub.png`: 진부면 물류 Hub.
5. `05-pyeongchang-town.png`: 평창읍 Town과 Hub→Town 회랑.

다섯 PNG는 그래픽 장치를 활성화한 Unity Editor 배치 실행에서 전용 Camera로 렌더링하고 육안 확인했다. 실제 Play Mode 입력이나 Game View 창 캡처, 서버 연결·화물 업무 완료 증거는 아니다. `-nographics` 실행에서 만들어진 빈 화면은 최종 그래픽 실행으로 덮어썼다.

## 검증

- Scene 회귀를 포함한 공간/법정동 집중 EditMode: 32/32 통과.
- 공간 LOD Loader 집중 PlayMode: 1/1 통과.
- 기존 전략 카메라 PlayMode 회귀 1건은 합성 `W` 입력 후 이동량이 0이어서 실패했다. 공간 Pipeline 상태 변경 실패는 아니지만 카메라 전체 회귀 통과로 보고하지 않는다.
- Unity Editor assembly build: 오류 0개. 기존 nullable·obsolete 경고는 유지된다.
- World Builder 실행: 성공, `SpatialPipeline_EPSG5186_TileAreaSet` 저장.
- 실제 WorldCover 대장: L0 42개, L1 437개, L2 6,150개, 유효 면적 1,464.4183㎢.
- 원본 Synty Prefab·Material·`.meta` GUID는 수정하지 않았다.
- 마지막 Synty 연결 Pipeline 집중 EditMode 17/17, 법정동 World·Shell 회귀 EditMode 18/18을 통과했다.
- commit·push·배포와 운영 API 호출은 수행하지 않았다.
