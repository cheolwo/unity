# WORLD-COMPLETION-AREA-1 대관령 Farm 1km 첫 화면

## 결과

`SimulationWorldShell` 안에 EPSG:5186 기준 1km × 1km 경관 완결 영역을 L2 500m 타일 2×2로 구성했다.

- `kr5186:l2:700:1144`: 농장 마당 기준작
- `kr5186:l2:701:1144`: 감자 경작지
- `kr5186:l2:700:1145`: 산림 전이
- `kr5186:l2:701:1145`: Farm 출발 회랑

큰 지형과 수목대, 중간 크기 농장·경작 군집, 작은 감자 표현·울타리·바위 순으로 배치했다. 시각 자산은 `VisualKey → 구성 대장 → Synty Prefab` 경계를 따르며 Synty 원본 Prefab과 Material은 수정하지 않았다.

## 화면 증거

![실제 Play Mode Game View](01-daegwallyeong-farm-1km-play-game-view.png)

![경관 품질 Pipeline 적용 1인칭 Play Mode Game View](06-daegwallyeong-farm-first-person-play-game-view.png)

![Synty 농부 플레이어 보행 Play Mode Game View](07-daegwallyeong-farm-player-walk-game-view.png)

![플레이어 눈높이 1인칭 WASD Play Mode Game View](08-daegwallyeong-farm-player-first-person-wasd-game-view.png)

![선택 강조된 3인칭 우클릭 이동 Play Mode Game View](09-daegwallyeong-farm-player-third-person-click-move-game-view.png)

![RTS 전술 3인칭 지휘 Play Mode Game View](10-daegwallyeong-farm-rts-tactical-command-game-view.png)

- `01-daegwallyeong-farm-1km-play-game-view.png`: UI를 잠시 숨기고 실제 Play Mode Game View에서 저장한 첫 화면
- `01-daegwallyeong-farm-1km-first-view.png`: 동일한 Farm 전용 증거 카메라의 1280×720 단독 렌더
- `02`~`05`: 전체 World, Farm→Hub 회랑, 진부 Hub, 평창읍 Town 연계 화면
- `06-daegwallyeong-farm-first-person-play-game-view.png`: 경관 품질 Profile을 적용하고 사람 눈높이에서 저장한 실제 Play Mode Game View
- `07-daegwallyeong-farm-player-walk-game-view.png`: Synty 농부 플레이어를 넣고 WASD 이동과 마우스 시선 회전 뒤 저장한 실제 Play Mode Game View
- `08-daegwallyeong-farm-player-first-person-wasd-game-view.png`: 플레이어 루트의 눈높이 카메라에서 F2 1인칭과 WASD 안내를 확인한 실제 Play Mode Game View
- `09-daegwallyeong-farm-player-third-person-click-move-game-view.png`: F3 3인칭에서 캐릭터 선택 강조와 우클릭 이동 안내를 확인한 실제 Play Mode Game View
- `10-daegwallyeong-farm-rts-tactical-command-game-view.png`: 스타크래프트·Company of Heroes 계열처럼 높은 사선에서 농장 건물·밭·진입로와 선택 유닛을 함께 읽는 F3 RTS 전술 화면

## 경관 품질 후처리

Synty 자산 연결 다음에 `L9_경관품질후처리_PresentationOnly` 단계를 추가했다.

```text
영역 역할·시간대·날씨 의미
→ 경관RenderingProfile
→ 태양·환경광·그림자
→ 하늘·안개·대기 원근
→ URP 색보정·Bloom·Vignette
→ Overview·Region·Task·1인칭 카메라
```

첫 Profile은 `rendering-profile:sim:pyeongchang:rural-clear-day.v1`이다. 태양 방향과 부드러운 그림자, 삼색 환경광, 절제한 선형 안개, Procedural Skybox, 색 대비·채도·Bloom·Vignette를 한 번에 적용한다. Volume 구성 요소는 Profile의 하위 자산으로 저장해 Asset 새로고침 뒤에도 유지한다.

1인칭 카메라는 눈높이 1.68m, 시야각 62°이며 WASD 이동, Shift 빠른 이동, 오른쪽 마우스 시선을 지원한다. 이동과 시선은 표현 전용이며 서버 상태와 Simulation Tick을 바꾸지 않는다.

## 플레이어 경관 탐색

`L10_플레이어경관탐색_PresentationOnly` 단계에는 다음 계층을 둔다.

```text
LegalWorldFarmPlayer
├─ CharacterController
├─ VisualRoot_SyntyFarmer
│  └─ PolygonFarm 농부 Humanoid Prefab
├─ FirstPersonPivot
│  └─ PlayerFirstPersonCamera
└─ 선택강조Ring

L10_플레이어경관탐색_PresentationOnly
├─ TacticalCameraPivot
│  └─ PlayerCamera
└─ 우클릭이동목적지Ring
```

- F2 1인칭: WASD·방향키로 직접 이동하고 Shift로 달리기
- 1인칭 Game View 클릭 후 마우스: 가로·세로 시선 회전
- F3 RTS 전술 3인칭: 52°의 높은 사선과 기본 거리 15.5m에서 농장 영역과 유닛을 함께 표시
- RTS 화면 WASD·방향키: 플레이어와 독립적으로 전술 카메라 초점을 지면 위에서 이동
- RTS 화면 휠: 9~23m 범위에서 확대·축소
- RTS 화면 F: 전술 카메라를 선택 유닛 위치로 재집중
- RTS 화면 캐릭터 좌클릭: 주황 원으로 선택·강조
- 선택된 3인칭 캐릭터: 지형을 우클릭하면 청록 목적지를 표시하고 해당 지점으로 이동
- RTS 화면 휠 클릭 드래그: 전술 카메라 시선 회전
- F1: 기존 전략 화면으로 복귀
- Esc: 마우스 고정 해제
- 지형 `MeshCollider`, 플레이어 `CharacterController`, 카메라 구체 충돌 검사로 지면과 카메라 가림을 처리
- 기존 공용 `Idle/Walk` 표현 어댑터를 재사용하되 이동은 업무 작업 완료를 뜻하지 않음

## 검증과 경계

- Unity Editor에서 `WORLD-LEGAL-2 평창군 Synty 경관 배치`를 실행해 Scene을 재생성·저장했다.
- Unity EditMode C# 프로젝트 정적 build는 오류 0개로 통과했다. 저장소에 이미 존재하던 nullable·obsolete 경고는 남아 있다.
- 실제 Play Mode에서 Farm 전용 카메라가 Game View를 렌더하는 것을 확인하고 PNG를 저장했다.
- 실제 Play Mode에서 1인칭 카메라, 따뜻한 조명, 그림자, 하늘과 후처리가 함께 렌더되는 것을 확인했다.
- 실제 Play Mode에서 Synty 농부가 Game View에 나타나는 것을 확인하고, 마우스 드래그로 시점을 회전한 뒤 `W`와 `Shift+W`로 전진시켜 추적 화면이 함께 이동하는 것을 확인했다.
- 실제 Play Mode에서 F2 1인칭 카메라가 캐릭터 몸을 숨기고 눈높이 화면을 렌더하는 것을 확인했다.
- 실제 Play Mode에서 F3 3인칭의 주황 선택 원, 지면 우클릭 뒤 청록 목적지 표식, 캐릭터 회전·이동을 확인했다.
- 실제 Play Mode에서 F3 카메라가 캐릭터 어깨를 따라가는 근접 시점이 아니라 독립 `TacticalCameraPivot`을 사용하는 높은 사선 RTS 화면으로 전환되는 것을 확인했다. 실제 마우스 휠로 줌 거리가 바뀌고, 좌클릭 유닛 선택 뒤 우클릭 지점까지 캐릭터가 이동하는 것도 다시 확인했다.
- 플레이어 EditMode·PlayMode 테스트 코드는 정적 build 오류 0개로 컴파일됐다. 연결된 Editor에서 PlayMode 자동 실행을 시도했으나 Unity Test Runner가 `Test tree is not available for PostbuildCleanupTask`로 중단되어 자동 실행 통과 증거로는 기록하지 않는다.
- Play Mode 중 기존 Turn closing 서버 호출은 `ConnectionError`였다. 따라서 이 화면은 서버 연결·운송 완료·생산 완료의 증거가 아니다.
- 실제 DEM, 세분류 토지피복 위치, 건물 도형과 배치 마스크는 아직 연결 전이다. 현재 높낮이와 감자밭은 각각 `ScenarioTerrainPreview`, `Scenario` 표현이다.
- 잘못 선택한 기존 `Build World Bootstrap Scene` 메뉴가 Play Mode에서 거부된 기록은 화면 생성과 무관하며 Scene 저장 결과를 바꾸지 않았다.

## 변경 범위

- `Assets/Ssalddel/Editor/대한민국법정동WorldBuilder.cs`
- `Assets/Ssalddel/Runtime/World/경관RenderingPipelineModels.cs`
- `Assets/Ssalddel/Presentation/World/플레이어경관Controller.cs`
- `Assets/Ssalddel/Presentation/World/경관품질PipelineView.cs`
- `Assets/Ssalddel/Tests/EditMode/대한민국법정동WorldTests.cs`
- `Assets/Ssalddel/Tests/PlayMode/PublicWorldMapPresenterPlayModeTests.cs`
- `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`
- 이 변경 기록과 PNG

commit, push, 배포는 수행하지 않았다.
