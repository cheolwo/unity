# Ssalddel Unity

농장에서 수확한 생산물이 물류 거점과 판로를 거쳐 도시로 이어지는 과정을, 추적 가능한 상태와 상호작용으로 표현하는 Unity 월드 프로젝트입니다.

![통합 SimulationWorldShell의 자료 조사 기반 L2 스트리밍 Game View](Documentation/Changes/2026-08-14-researched-l2-stream-window/l2-researched-window-game-view.png)

_현재 대표 화면 — 농장 1인칭에서 3×3 상세·5×5 활성·9×9 준비 창, 방향 선행 중심과 시야 기반 프록시→Synty 상세 승격을 진단 트리로 확인한 실제 Play Mode Game View_

## 현재 구현 범위

- 감자 재배·수확부터 포장·상차, 물류 거점 입고·검수, 판로 분배와 도시 도착까지의 연구 Scene
- 생산자 조합 출하, 온라인 직접 판매, 비축 보관, 외부 교역 준비의 명시적 판로 선택
- 서버 revision과 WorldTick을 기준으로 한 Preview → Confirm → 적용 흐름
- EPSG:5186 기준 L0 8km·L1 2km·L2 500m 타일과 Farm·Hub·Town 영역 및 AreaSet 구성
- 플레이어 위치를 따라 L2 500m 타일을 3×3 상세·5×5 활성·9×9 준비 창으로 관리하고, 경계 125m 안에서 이동 방향 쪽으로 준비 중심을 앞당기는 동적 스트리밍
- 카메라 실제 시야·화면 여백·이동 예측을 따라 건물을 프록시→Synty 상세로 승격하고 화면 밖 표현을 캐시하는 시야 스트리밍
- 준비되지 않은 타일로의 이동을 안전 경계에서 멈추고 수평 타일 창·수직 자료 처리·건물 승격을 한눈에 보여주는 런타임 진단 트리
- 법정동 경계·표고·토지피복·통계를 분리한 평창군 공간 생성 계약과 결정적 경관 배치 계획
- 대관령면 Farm → 진부면 Hub → 평창읍 Town을 잇는 시나리오 회랑과 Synty 의미 기반 경관
- 1인칭 WASD 탐색과 RTS형 전략 카메라·지점 이동을 함께 제공하는 플레이어 조작
- 시뮬레이션 상태 사본에 따라 이동·작업을 표현하는 역할 캐릭터와 NPC 업무 행동
- Figma·MAUI 창고 디자인 의미를 공유하고 검수→적재 Preview·Confirm·WorldTick을 실제 기능에 연결한 진부면 입고 정보판
- `SimulationWorldShell` 하나에서 월드 개요·농장 1인칭·농장 전술·진부 입고를 전환하는 상시 화면 전환 막대
- 정보 패널의 접기·펼치기·닫기·다시 열기 상호작용
- `감자생산유통`, `생산자판로`, `에셋연구` 맥락별 Scene 구성

## 한눈에 보는 전체 구조

아래 트리는 공공 공간자료와 시뮬레이션 상태가 Unity 화면으로 표현되는 순서를 보여줍니다. 위쪽은 사실과 규칙을 담당하고, 아래쪽으로 갈수록 교체 가능한 시각 표현을 담당합니다.

```text
공공 공간자료·운영 데이터                    [Unity 바깥의 원본 사실]
├─ 법정동 경계·DEM·토지피복·면적 통계
├─ 건물·사업체·인허가 등 출처가 있는 공간 정보
└─ Hongdal 서버의 업무 원장과 시뮬레이션 규칙
   └─ 상태 사본·revision·WorldTick·관점별 조회 결과
      ↓
Ssalddel Unity
├─ Data / Simulation                         [사실 수신과 게임 규칙]
│  ├─ 서버 권위 상태 사본
│  ├─ 공간 원본의 출처·기준일·CRS·해시
│  ├─ NPC 조직·역량·업무 배정·행동 단계
│  └─ Preview → 명시적 Confirm → 갱신 상태 재조회
│
├─ 공간 생성 Pipeline                       [어디에 무엇을 둘 수 있는가]
│  ├─ EPSG:5186 고정 타일
│  │  ├─ L0 8,000m : World Overview
│  │  ├─ L1 2,000m : Region Focus
│  │  └─ L2   500m : Task Focus
│  │     └─ Runtime : 플레이어 주변 3×3 상세 / 5×5 활성 / 9×9 준비
│  │        ├─ 경계 선행 : 125m 안에서 이동 방향 쪽 중심 한 칸 이동
│  │        ├─ 요청 예산 : 동시 타일 로드 4개 / 기존 Slot 재사용
│  │        ├─ 이동 안전 : 추적 타일 + 지면 + 안전 기반 Layer
│  │        └─ 시야 우선 : 절두체 + 화면 여백 + 이동 예측
│  ├─ Layer
│  │  ├─ 법정동 경계
│  │  ├─ PhysicalElevation / VisualElevation
│  │  ├─ 토지피복·수계·경사·배치 가능 마스크
│  │  └─ 면적 배분 결과 → 경관 구성 계획
│  ├─ Area : Farm / Hub / Town
│  ├─ Link : 공식 도로 또는 SimulationRoute
│  └─ AreaSet : 여러 Area와 Link를 묶은 시나리오 단위
│
├─ 현재 대표 AreaSet                        [pyeongchang-farm-hub-town-v1]
│  ├─ 대관령면 Farm
│  │  └─ 1km 완결 영역 = L2 500m 타일 2×2
│  ├─ Farm–Hub SimulationRoute
│  ├─ 진부면 Hub
│  │  └─ NPC 입고 검수·보관 가능 상태 표현
│  ├─ Hub–Town SimulationRoute
│  └─ 평창읍 Town
│
├─ Perspective / PresentationModel          [Unity가 표현할 상태로 번역]
│  ├─ 지역·업무·선택 상태 Presenter
│  ├─ 역할 캐릭터·화물차·NPC 행동 View
│  ├─ Figma·MAUI 의미 키 → Unity Theme·상태 배지·업무 정보판
│  └─ 1인칭·RTS 카메라, LOD 전환과 동적 타일·건물 생명주기
│
└─ VisualRoot                               [교체 가능한 화면 표현]
   ├─ VisualKey / 시각 자산 대장
   ├─ Synty Farm·Town·City·Generic·Starter Prefab
   ├─ 지형·건물·수목·작물·도로·화물 소품
   ├─ 건물 상태 : Declared → Proxy → Synty Detail → HiddenCached
   └─ URP 경관 품질
      ├─ Material variant / MaterialPropertyBlock
      ├─ 조명·그림자·안개·후처리
      └─ LOD / Cluster / HLOD / 렌더링 비용 예산
```

## Unity 프로젝트 길잡이

```text
Assets/Ssalddel
├─ Runtime
│  └─ World                         타일·AreaSet·법정동·NPC·렌더링 계약
├─ Application                     Unity에서 실행하는 사용 사례
├─ Infrastructure
│  └─ Simulation                    서버 상태 사본을 읽는 데이터 연결
├─ Presentation
│  └─ World
│     ├─ *Presenter                 상태를 화면 표현으로 번역
│     ├─ *View / *Controller        Scene 배치와 사용자 조작
│     ├─ *VisualCatalog             의미 기반 시각 자산 대장
│     └─ Generated                  생성된 Mesh·Material·Volume 자산
├─ Editor
│  ├─ 대한민국법정동WorldBuilder.cs  평창군 월드와 Pipeline Scene 구성
│  └─ 공간TileStreamingBuilder.cs     통합 Scene의 동적 타일·상태판 배선
├─ Scenes
│  ├─ SimulationWorldShell.unity    유일하게 활성화된 통합 Play Scene
│  └─ WorldBootstrapScene.unity     비활성 공공데이터 관찰·검증 Scene
├─ Tests
│  ├─ EditMode                      계약·배선·결정성 검증
│  └─ PlayMode                      실제 입력·카메라·표현 검증
└─ Experiments - 연구              검증 전 연구 Scene과 에셋 실험
```

새 기능은 가능한 한 `Runtime 계약 → Application/Infrastructure 연결 → PresentationModel → VisualRoot` 순서로 추가합니다. 연구 Scene에서 검증된 결과는 `SimulationWorldShell`에 점진적으로 통합하며, 기능마다 별도의 최종 Play Scene을 늘리지 않습니다.

실행할 때는 Build Settings에서 활성화된 `SimulationWorldShell` 하나로 진입합니다. `WorldBootstrapScene`과 `Experiments - 연구`의 Scene은 회귀 검증과 연구 근거로 보존하지만 독립 게임 진입점으로 사용하지 않습니다.

통합 Scene을 다시 만들 때는 Unity 메뉴 `Ssalddel > 통합 월드 > 전체 재생성`을 사용합니다. 현재 Scene 배치를 보존하면서 전환 UI와 Build Settings만 다시 연결하려면 `기존 Scene 통합 배선과 Build Settings 적용`을 사용합니다.

## 프로젝트 경계

현재 화면과 데이터는 개발용 Simulation입니다. 실제 판매, 결제, 배차, 수출, 정산을 실행하지 않으며 운영 상태의 최종 권위는 서버에 둡니다.

- `PhysicalElevation`은 경사·수계·배치 판정에 사용하고, 보기 좋은 높이 과장은 `VisualElevation`에서만 적용합니다.
- Synty Prefab과 URP 효과는 `PresentationOnly`이며 법정동 코드, 업무 상태, 완료 여부를 변경하지 않습니다.
- NPC와 차량의 이동·애니메이션은 상태 표현입니다. 실제 업무 완료의 근거는 서버 상태 사본의 revision과 WorldTick입니다.
- 진부면 입고 정보판의 기본 Scene은 Simulation 서버를 사용합니다. fixture는 자동 대체 경로가 아니라 테스트와 Game View 증거용으로만 명시적으로 주입합니다.
- 현재 평창군 지형 Mesh는 `ScenarioTerrainPreview`이며 실제 DEM 기반 산출물로 교체해야 하는 상태를 Scene 이름과 검증 기록에 명시합니다.
- 동적 타일 Fixture는 실제 DEM·토지피복·배치 마스크 URL이나 높이를 꾸며내지 않고 `WaitingForSpatialArtifact` 경계만 표시합니다. 경계에는 Collider가 없으며 공공 공간자료 완료 증거가 아닙니다.

진행 과정과 Game View 기록은 [`Documentation/Changes`](Documentation/Changes/README.md)에서 확인할 수 있습니다.
