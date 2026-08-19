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

## 한눈에 보는 상향식 세계 구축 구조

공공데이터가 먼저 세계를 결정하지 않습니다. 게임 플레이와 세계 의도에서 WI와 H 공간을 상향식으로 조립하고, AreaSet이 요구한 현실 근거만 E6에서 연결합니다.

```text
게임 기획과 세계 의도
│
├─ 플레이어 경험
│  ├─ Nature 체류·탐험·위협·회복
│  ├─ Farm 생산·수확·출하
│  ├─ City/Hub 물류·검수·보관
│  └─ Town 시장·생활·소비
│
├─ WI 세계 상호작용 단위
│  ├─ 누가 무엇을 하는가
│  ├─ 어떤 상태에서 시작하는가
│  ├─ 어떤 공간 능력이 필요한가
│  ├─ 무엇을 예약하는가
│  └─ Task·Effect 뒤 무엇이 달라지는가
│
└─ 상향식 공간 설계 재고
   ├─ 기준 경관 문법
   │  └─ 52개 의미군 × A/B/C = 156개 표현 변형
   ├─ H1 작업공간 모판
   │  └─ 생산구획·작업마당·검수영역·보관공간·회복공간
   ├─ H2 블록 모판
   │  └─ 여러 H1의 상대 배치·내부 동선·입구·출구
   ├─ H3 경관 모판
   │  └─ 여러 H2의 Node·Edge·Connector 이동 폐루프
   └─ H4 지역 모판
      └─ Nature·Farm·City/Hub·Town 세계 의도
         ↓
이론 공간 생산 공장
├─ H2 TheoryQualified 24개
├─ H3 TheoryQualified 13개
└─ E5TheoryQualified AreaSet 4개
   ├─ Nature 생활·탐험권
   ├─ Farm 생산·후처리권
   ├─ City/Hub 물류권
   └─ Town 생활·시장권
      ↓
AreaSet 세계 설계
├─ 어떤 지역 세계를 만들 것인가
├─ 어떤 LandscapeGraph가 필요한가
├─ Graph 사이를 어떻게 연결하는가
└─ 어떤 WI를 어느 공간에서 수용하는가
      ↓
LandscapeGraph 공간 조립
├─ 공간 Node
├─ 이동 Edge
├─ 외부 Connector
├─ 공간 역할
├─ 공간 능력
└─ 업무 용량
      ├──────────────────────┐
      ▼                      ▼
Simulation 서버             E6 현실 근거
                             ├─ DataRequirement
                             ├─ EvidenceBinding
                             ├─ DerivedArtifact
                             └─ 공공데이터 계보
```

## Simulation 서버와 Unity 실행 구조

```text
LandscapeGraph 공간 정의 + WI 공간 요구 + Simulation 규칙
        │
        ▼
Ssalddel.Simulation 서버
│
├─ Contracts
│  ├─ HTTP 요청·응답
│  ├─ Session 상태 사본
│  ├─ WI Preview·Confirm 계약
│  └─ World Streaming·경관 조회 계약
│
├─ Domain
│  ├─ Session·WorldTick·WorldRevision
│  ├─ Decision·Task·Effect
│  ├─ 공간 예약
│  └─ 공간 Runtime 상태
│
├─ Application
│  ├─ WI 규칙 실행
│  ├─ 행위자·공간·자원 조건 판정
│  ├─ 공간 후보 결정
│  ├─ Preview·Confirm·예약 조율
│  └─ Snapshot 생성
│
├─ Infrastructure / Persistence
│  ├─ Session 저장·Save / Replay
│  ├─ LandscapeGraph·공간자료 파생 DB 조회
│  └─ 멱등 Command·Revision 검증
│
└─ Server API
   ├─ Preview·Confirm·WorldTick·Task 취소
   ├─ 최신 상태 재조회
   ├─ AreaSet·LandscapeGraph 조회
   └─ World Streaming
        │
        ▼
Unity 데이터 연결
├─ 서버 상태 사본·WI 진행 상태 수신
├─ AreaSet·LandscapeGraph 수신
├─ H1·H2·H3 공간 구성 수신
└─ revision·WorldTick·해시 검증
        │
        ▼
Unity Application
├─ 플레이어 입력을 서버 명령 후보로 번역
├─ Preview 요청·명시적 Confirm
├─ Tick 진행 요청
└─ 최신 상태 재조회
        │
        ▼
Unity Presentation
├─ H1 작업공간·H2 블록·H3 경관·H4 지역 전환
├─ NPC 배정·이동·작업·완료·차단·회복 표현
├─ Nature·Farm·City·Town Synty 표현 연결
├─ Construction 공통 기능층
└─ canonical Scene: SimulationWorldShell
   ├─ 3인칭 주인공과 Nature 상시 체류 세계
   ├─ Farm·City/Hub·Town 업무 영역
   └─ 경관·업무·상태 정보판
        │
        ▼
E7 실제 플레이 검증
├─ 실제 서버 HTTP·서버 상태 재조회
├─ 저장 Scene·Play Mode·Game View
└─ Save / Replay
```

Simulation 서버는 무엇이 실제로 일어났는지 결정하고, LandscapeGraph는 어디에서 무엇이 가능한지 제공합니다. Unity는 결정된 공간과 사건을 표현하며 Synty Prefab은 표현 재료입니다. GameObject·Animator·NavMesh 상태는 업무 완료 권위가 아닙니다.

## E와 H의 현재 진행 트리

```text
WI 세계 상호작용
├─ E1 행위·계약 확정
├─ E2 코드 구현
├─ E3 자동 시험 통과
│  └─ 현재 WI 41개
├─ E4 위치 독립 공간 모판 실행 검증
├─ E5 공간 결속
│  ├─ H1 작업공간
│  ├─ H2 블록
│  ├─ H3 이동 경관
│  └─ H4 AreaSet 세계
│     └─ 현재 자동 생산 결과
│        ├─ H2 TheoryQualified 24개
│        ├─ H3 TheoryQualified 13개
│        └─ E5TheoryQualified AreaSet 4개
├─ E6 현실 근거 결속
│  ├─ 공공데이터 요구·실제 근거
│  ├─ 파생 공간자료
│  └─ 데이터 계보
└─ E7 실제 플레이 폐루프
   ├─ 실제 Simulation 서버·LandscapeGraph
   ├─ SimulationWorldShell·Play Mode·Game View
   └─ Save / Replay
```

`E5TheoryQualified`는 사람 검토 없이 이론상 공간 구조와 연결이 닫힌 상태입니다. 실제 지역·공공데이터·Unity Runtime 증거는 아니며 E6·E7과 구분합니다.

## Unity 프로젝트 길잡이

아래 트리는 현재 존재하는 폴더를 기준으로 책임을 설명합니다. H4와 이론 AreaSet은 서버 설계 입력이며 Unity 폴더 자체를 새 권위로 만들지 않습니다.

```text
Assets/Ssalddel
├─ Runtime
│  ├─ World                       AreaSet·LandscapeGraph·H 공간·Streaming 모델
│  ├─ WorldMap                    지도·지역 관점 모델
│  ├─ Ledgers                     상태 사본과 원장 모델
│  ├─ Transport                   이동·화물 표현 계약
│  └─ Configuration              실행 설정
├─ Application
│  ├─ Bootstrap                   통합 실행 조율
│  ├─ WorldMap                    공간 선택·조회 사용 사례
│  └─ Exhibition                 전시·검토 사용 사례
├─ Infrastructure
│  ├─ Simulation                  Simulation HTTP·상태 사본 데이터 연결
│  ├─ WorldMap                    AreaSet·Graph·Streaming 데이터 연결
│  ├─ Transport                   이동 데이터 연결
│  └─ UrbanMarket                 시장 업무 데이터 연결
├─ Presentation
│  └─ World
│     ├─ Presenter                서버 상태를 화면 표현으로 번역
│     ├─ View·Controller          Scene 배치와 사용자 조작
│     ├─ H 공간 Root             H1·H2 조합과 경관 인스턴스 표현
│     ├─ VisualCatalog           의미 기반 Synty 시각 자산 대장
│     └─ Generated               생성된 Prefab·Mesh·Material·Volume
├─ Editor
│  ├─ H1·H2·H3 조립과 고정 시점 촬영
│  ├─ 월드·타일·경관 구성 생성
│  └─ SimulationWorldShell 통합 배선
├─ Scenes
│  ├─ SimulationWorldShell.unity  유일한 공식 Play 진입 Scene
│  └─ WorldBootstrapScene.unity   비활성 공공데이터 관찰·검증 Scene
├─ Tests
│  ├─ EditMode                    계약·배선·결정성 검증
│  └─ PlayMode                    실제 입력·카메라·표현 검증
└─ Experiments - 연구
   ├─ Synty 팩 탐색
   ├─ H 조합 실험
   └─ 경관·전투·카메라 연구

Documentation
├─ Changes                        장기 보존 변경·Game View 기록
├─ H 조합물 촬영 기록
└─ 서버 전송·검토 기록
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
