# Three Region 300m Spacing and Transition Landmarks

## Result

`ThreeRegionHubJourney`의 기존 논리 좌표와 Presentation 구성을 유지하면서 X/Z 배치만 `6.8`배 확장했다. 측정된 중심 간 거리는 다음과 같다.

- Farm → Town: 약 292m
- Farm → Regional Logistics Hub: 약 342m
- Town → Regional Logistics Hub: 약 362m
- Regional Logistics Hub → City: 약 286m

전체 간격을 한 화면에 보여 주는 World 초점과 각 Region을 확인하는 Zone 초점을 분리했다. 카메라 초점과 조형물은 Presentation 전용이며 서버·Simulation 업무 상태를 결정하지 않는다.

## Transition landmarks

- Farm → Town: 풍차, 우물, 건초, 벤치
- Farm → Hub: 급수탑, 바위, 수목 군집
- Town → Hub: 피크닉 테이블, 버스 정류장, 화분
- Town → City: 공원 벤치, 가로수, 버스 정류장
- Hub → City: 물류 Station, 가로등 4개, 화분

총 18개 이상의 Synty 환경 오브젝트를 `ART1 Transition Landmarks` 아래에 배치했다. 원본 Synty prefab은 수정하지 않고 기존 Visual Catalog를 통해 인스턴스화한다.

## Game View evidence

### World overview

![Three region 300m overview](three-region-300m-game-view.png)

### Regional logistics hub focus

![Hub transition landmarks](hub-transition-landmarks-game-view.png)

## Verification

- `세RegionHubJourneyTests` + `DioramaCameraTests`: 15/15 passed
- 결과: `artifacts/local/validation/three-region-300m-final-editmode.xml`
- Play Mode/Game View에서 World overview와 Hub focus를 각각 캡처함

## Remaining visual work

300m 완충 구간의 지형 색면과 식생 밀도는 아직 1차 기준선이다. 다음 미술 패스에서는 도로변 수목·울타리·경사·작은 휴게 지점을 반복 간격 규칙으로 보강하고 모바일 draw call과 LOD를 함께 측정한다.
