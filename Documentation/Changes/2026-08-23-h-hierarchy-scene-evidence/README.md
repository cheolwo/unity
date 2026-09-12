# H1~H3 공식 Scene 배치 증거

## 결과

이론으로 관리하던 H1~H3 기준안을 공식 플레이 장면
`Assets/Ssalddel/Scenes/SimulationWorldShell.unity`에 배치했다.
이 배치는 공간 계층의 조립·표현 가능성을 검증하는 `PresentationOnly` 증거이며,
Simulation 또는 운영 상태의 권위를 만들지 않는다.

## 입력과 배치 규칙

- H5 입력: Hongdal `eng/world-seedbeds/generated/h5-world-layout.v1.json`
- H1~H3 입력: `AreaSetCompositionPatternReview.unity`의 4개 Baseline
- 배치 정책: `FarmCenteredScenarioLocalMeters`
- 좌표 축척: H5 시나리오 미터 × `0.24` Unity unit
- AreaSet: NatureHome, Farm, Town, CityHub 각 1개
- 배치 결과: H3 16개, H2 44개, H1 표현 125개
- 전이 회랑: 3개

저장 장면의 `H계층실증Root_PresentationOnly`에 입력 revision/hash, 배치 정책,
AreaSet 참조와 개수를 직렬화해 다시 검증할 수 있게 했다.

## 공간 배치 규칙

`h-spatial-placement-rules.v1`은 다음 순서로 배치를 제한한다.

1. H5에서 가장 가까운 AreaSet 중심 거리의 36%만 H4 핵의 허용 반경으로 사용한다.
2. H3 바닥 조각도 같은 반경 안으로 정규화해 다른 AreaSet 바닥 침범을 줄인다.
3. 실제 Renderer 발자국을 측정해 H1끼리 최소 0.55 unit을 띄운다.
4. 건물과 산 능선은 최소 3 unit을 띄우고, 능선 표현 폭은 최대 12 unit으로 제한한다.
5. 전역 산악 Skybox는 AreaSet 배치 객체로 사용하지 않는다.
6. 울타리 segment는 묶음별 연결 그래프가 최대 1.5 unit 간격 안에서 이어지는지 검사한다.
7. Player 주변에는 감자 생산·정비·작업·수확·종자 준비 5개를 작업 고리로 배치한다.

최종 기록은 H1 125개 중 74개 재배치, 발자국 정규화 11개,
울타리 연속 묶음 6개, Player 작업 고리 5개, 규칙 위반 0건이다.
현재 바닥은 평탄 H3 실증 바닥이므로 건물 경사는 0도로 판정한다.
실제 E5/E6 지형에서는 이 값을 DEM 기반 `slope`·`placement-mask` 표본으로
교체해야 한다.

## 배치 통제 계층과 Player 비율

`placement-control-hierarchy.v1`은 H1~H5를 늘리는 새 공간 계층이 아니다.
다음 여섯 단계로 각 배치 객체의 허용 범위와 크기만 통제한다.

`세계 배치 외곽 → AreaSet 배치 외곽 → H3 경관 외곽 → H2 블록 외곽
→ H1 객체 발자국`, 그리고 별도 `Player 기준 크기`

- 모든 H1 기록은 가장 가까운 H3·H2 노드 이름과 크기 등급을 가진다.
- 건물·울타리·능선·일반 표현은 Player 실측 높이에 대한 높이·수평 발자국
  비율 범위 안에서만 승인된다.
- 원본 Synty 계층은 자산 계보를 위해 유지하고, 배치 통제 계층은 결정된
  소속과 비율을 참조 기록으로 보존한다.
- 이번 Scene의 Player 실측 높이는 1.939 unit이다. 건물 최대 높이는
  Player의 1.408배, 최대 수평 발자국은 4.200배다.

## H3→H2→H1 도로변 전면 배치

`placement-pattern:roadside-frontage-homestead.v1`은 계층 이름만 기록하는
표식이 아니라 상위 의미가 하위 모듈의 조립 규칙을 결정하는 첫 패턴이다.

- H3 `H3_HomeProcessing`: 생활·가공 경관의 도로 축을 X축으로 결정한다.
- H2 `H2_h2-candidate:farm-worker-support`: 도로 가장자리, 울타리 선,
  출입구 공백과 주택 후퇴 거리를 결정한다.
- H1: 기존 `농촌도로 직선`과 `타운 기본주택` 구성 모듈, Town 흰색 울타리
  두 개를 재사용하고 각 원본 구성 키를 패턴 증거에 보존한다.
- 측정 결과: 출입구 공백 2.600 unit, 도로–울타리 여유 0.420 unit,
  건물 후퇴 4.243 unit이며 출입구에서 도로까지의 경로가 열려 있다.

Town 주택 Prefab의 재사용은 Farm 작업자 지원 구획의 시각 표현 선택일 뿐
Town AreaSet의 규칙이나 Simulation 권위를 Farm으로 옮기지 않는다.

## 화면 정책

Play Mode의 기본 화면에는 Player, 시점 전환 UI와 승인된 H 환경만 남긴다.
H3/H2 경계, 연결점, WI 동선 등 제작 검토 오버레이는 기본으로 숨긴다.
승인된 환경 안에서 의도적으로 꺼 둔 Renderer도 단일 Player 보기 갱신이
다시 켜지 않도록 했다.

![Play Mode Game View](h-hierarchy-game-view.png)

## 증거 한계

- 저장된 공식 Scene 배치와 실제 Play Mode Game View를 확인했다.
- H3 바닥은 충돌체를 갖지만 실제 DEM·도로·수계·건물 자료나 NavMesh의
  생성·경로 통과 증거는 아니다.
- H1 표현은 시각 배치 증거이며 모든 상호작용 행동이 연결됐다는 뜻은 아니다.
- 로컬 서버가 실행되지 않은 상태라 서버 연결 오류가 Console에 남았다.
  이는 이 로컬 표현 검증과 분리된 미검증 범위다.
