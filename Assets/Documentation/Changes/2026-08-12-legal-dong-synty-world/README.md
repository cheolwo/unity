# 평창군 법정동 Synty World 첫 적용

## 적용 범위

- VWorld 법정동 경계 원본에서 단순화한 평창군 8개 읍·면 다각형을 `RegionCell` 공간 근거로 사용했다.
- 대관령면은 Farm, 진부면은 Hub, 평창읍은 Town이라는 `SimulationScenario` 역할을 부여했다. 이 역할은 법정동의 공식 속성이 아니다.
- 대관령면 Farm → 진부면 Hub를 첫 미술 세로 단위로 삼아 Synty Farm·Town·City 자산을 의미 기반 `VisualKey`와 `VisualRoot`를 통해 배치했다.
- 기존 대표점과 개략 연결선은 검증용으로 보존하고 기본 Game View에서는 숨겼다.

## 자료와 권위 경계

- 법정동 경계: VWorld, 기준일 2026-07-01, 원본 좌표계 EPSG:5186.
- 표고: 실제 DEM을 아직 확보하지 않았으므로 `Incomplete`인 연속 시나리오 지형이다.
- 토지피복: 실제 세분류 자료를 아직 확보하지 않았으므로 `Incomplete`인 배치 의미 마스크다.
- Farm·Town·Hub 역할, 작물·시설·경관 개체는 `SimulationScenario`이며 공식 지역 속성이나 생산 사실이 아니다.
- Synty Prefab과 움직임은 모두 `PresentationOnly`이고 서버 상태나 시뮬레이션 완료를 변경하지 않는다.
- Synty 원본 Prefab과 원본 Material, `.meta` GUID는 수정하지 않았다.

## 실제 Play Mode / Game View 증거

1. `01-World-Overview.png`: 평창군 전체 연속 지형과 Farm·Town·Hub 경관 덩어리.
2. `02-Daegwallyeong-Farm.png`: 감자밭, Barn, Silo, Farmhouse, Tractor, 풍차와 산림 가장자리.
3. `03-Farm-Hub-Corridor.png`: 곡선 농로, 울타리, 수목대와 Farm→Hub 전환 경관.
4. `04-Jinbu-Hub.png`: Station, 회차 공간, Van, Pallet, Cargo Box와 완충 수목대.

네 화면은 저장된 `SimulationWorldShell`을 Play Mode로 실행하고 전용 증거 카메라로 실제 Game View를 캡처했다. 법정동 Builder와 경관 표현 자체의 새 오류는 확인되지 않았지만, 전체 Console 이력에는 로컬 Simulation 서버 미실행에 따른 기존 턴 마감 연결 오류와 테스트 수집기 파일 잠금 오류가 남아 있다.

## 검증 상태와 남은 범위

- Unity Editor 스크립트 재컴파일은 `up_to_date`, 컴파일 실패와 컴파일 오류 0건이며 법정동 World Builder 실행도 완료했다.
- 생성 결과에서 8개 `RegionCell`, 숨겨진 법정동 경계, Farm→Hub 회랑과 네 증거 카메라를 확인했다.
- Unity 테스트 수집기는 기존 다른 PlayMode 결과를 반환했고, Editor가 열린 상태에서는 두 번째 Unity 인스턴스를 실행할 수 없었다.
- 생성된 `.csproj`의 `dotnet test`는 결과 파일을 만들지 않아 새 EditMode 시험 통과 근거로 사용하지 않는다.
- 실제 DEM·토지피복을 받아 교체한 뒤 경사·수계·허용 토지피복 배치 검증을 다시 수행해야 한다.
- 현재 Overview는 첫 세로 단위의 경관 덩어리를 확인하는 화면이며, 평창군 전역을 완성도 있게 채운 최종 경관은 아니다.
