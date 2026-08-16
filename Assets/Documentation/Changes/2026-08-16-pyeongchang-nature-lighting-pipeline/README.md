# 평창 네이처 경관 조명·명암 Pipeline

## 적용 결과

- `SimulationWorldShell`에 적용할 맑은 늦은 오전 렌더링 Profile v2를 정의하고 해시를 정적 경관 계획·검토 기록·Staging 영수증에 함께 봉인했다.
- 태양 색·강도·각도, 4단 그림자, 환경광, 안개, 색 보정, Bloom, Vignette와 SSAO 값을 하나의 Profile에서 PC URP 설정과 World Builder가 함께 읽도록 정리했다.
- Nature wrapper마다 `cast-receive`, `receive-only`, `disabled` 그림자 정책을 연결했다. 숲 군집은 투사·수신, 숲 가장자리는 수신 전용, 원경 능선과 FX는 그림자 비활성이다.
- 대관령 Farm의 활엽·침엽·혼효 군집과 가장자리·능선은 유지하고, 원거리 회랑과 Hub의 중복 고비용 군집 세 개를 줄였다.
- 울타리를 농로와 평행한 측방 위치로 옮겨 겹침 후보를 없앴다.
- 기본 계획은 8개 구획, 배치 132건, 의미 구성 34건이며 이 중 Nature 구성은 24건이다.

## 검증 결과

- `WORLD-PLAN-1`: 기본·보정·검토 JSON 재생성 성공.
- `WORLD-PLAN-2`: 8개 Staging Prefab 생성 성공, 오류 0건, 겹침 0건.
- 고정 성능 예산: 삼각형 213,839/222,412, Material Slot 387/408, Draw Call 387/408, Shadow Caster 243/260, Collider 195/213, Animator 5/7.
- 남은 경고 6건은 성능 예산 80% 접근 경고 5건과 실제 DEM이 아닌 `ScenarioPreview` 높이 근거 고지 1건이다.
- 렌더링 Profile 집중 EditMode 8/8, Nature 구성·그림자 정책 8/8, 정적 경관 계획·Staging·검토 관문 12/12 통과.

## 실제 Play Mode·Game View 점검

- Unity 6000.5.6f1 DX12 Editor에서 저장된 `SimulationWorldShell`을 열고 Play Mode에 진입했다.
- 산·농장·경작지 Overview는 Game View에 렌더링됐고, 편집 상태에서 청록색으로 과도하게 밝았던 수목은 Play Mode에서 정상 색조로 돌아왔다.
- 좌측 상단과 우측 하단의 큰 검은 UI 면은 Play Mode에서도 남아 경관을 가렸다.
- Game View에 초점을 둔 뒤 `F2`, `F3`, `W`를 입력했지만 1인칭·3인칭 전환과 이동은 발생하지 않았다. 저장 Scene과 참조 Prefab에서도 `플레이어경관Controller` script GUID를 찾지 못했다.
- Play Mode 시작 시 Console 오류 4건을 확인했다: `UnifiedWorldModeWiringMissing`, `TurnClosingServerRequestFailed:ConnectionError`, `SimulationInboundUiServerSessionMissing`, `전투시점Controller.SetAuthorityFailure`의 `NullReferenceException`.
- 확인 뒤 Play Mode를 종료했으며 저장 Scene을 저장하거나 교체하지 않았다.

## 검증 경계

- 검토 기록은 `Draft`이며 `CanStage=true`, `CanApply=false`다.
- `WORLD-PLAN-3`는 실행하지 않았고 기존 `SimulationWorldShell` 저장 Scene을 교체하지 않았다.
- 코드·시험·Staging 구조 검증 완료. Play Mode·Game View는 실행했지만 위 오류와 시각 이상 때문에 통과로 판정하지 않는다.
- 이번 Game View는 승인 전 저장 Scene을 검증한 것이며, 새 132건 Staging 배치와 렌더링 Profile v2가 Scene에 적용된 최종 화면 증거는 아니다.
- 실제 DEM Mesh·수계 마스크·HLOD bake와 실행 중인 서버 연결은 이번 범위가 아니다.
- commit·push·배포는 수행하지 않았다.

## 후속 결함 보완 재검증

- 저장 Scene에는 `플레이어경관Controller`가 있었지만 경관 재생성 뒤 통합 모드·농장 경영·전투 구성의 플레이어 참조가 `null`로 남아 있었다. `기존 Scene에 모드 전환 UI 연결`을 다시 실행해 현재 `LegalWorldFarmPlayer`로 배선을 직렬화했고, 같은 순서가 반복돼도 현재 Scene 참조를 다시 찾도록 실패 안전 처리를 추가했다.
- `전투시점Controller.SetAuthorityFailure`는 플레이어 참조가 없을 때도 `NullReferenceException`을 만들지 않는다. 서버 세션을 얻기 전에는 `TurnClosingPanel`을 숨기고, 성공적으로 기준 상태를 읽은 뒤에만 다시 표시한다.
- Game View를 `1.5x`로 확대했을 때 좌측 상태 UI가 잘려 검은 면처럼 보였다. `1x`에서는 상태·모드·navigation 문구가 정상 렌더링되며, 우측 하단의 빈 턴 패널은 서버 연결 실패 뒤 표시되지 않는다.
- 저장 Scene 기반 EditMode는 통합 월드 3/3, 전투 입력 3/3, 턴 마감 5/5, 농장 경영 시점 3/3이 통과했다. PlayMode는 버튼 기반 통합 전환과 Input System `F2 → W 유지 → F3` 시점·이동 시험 2/2가 통과했다.
- Unity 6000.5.6f1 DX12 실제 Play Mode에서 Overview와 1인칭 전환을 다시 확인했다. `UnifiedWorldModeWiringMissing`과 `SetAuthorityFailure`의 `NullReferenceException`은 재발하지 않았다. 실행 중인 서버가 없으므로 턴 마감 연결, 입고 UI 세션, 농장 전투 세션 오류 3건은 별도 서버 미연결 상태로 남는다.
- 최종 1인칭 Camera Game View 증거는 `01-simulation-world-shell-wiring-repaired-game-view.png`다. 이 화면도 승인 전 기존 Scene 증거이며 Draft 상태의 132건 Staging·렌더링 Profile v2 최종 적용 증거가 아니다.

## 승인 적용과 최종 Play Mode 재검증

- 위의 `Draft`·미적용 기록은 최초 점검 당시의 이력이다. 현재 검토 기록은 `ApprovedForSceneApply`이고 기획서·기본·보정·병합 계획·렌더링 Profile 해시가 모두 일치한다.
- 통합 Scene 재조립 과정에서 빠졌던 8개 정적 경관 Anchor를 기존 L2 타일·회랑·경관 루트에 멱등 복구하도록 `WORLD-PLAN-3`을 보완했다. 적용 뒤 Anchor 8/8, 생성 Root 8/8을 확인했다.
- 플레이어 시작점과 겹치던 회랑 울타리 하나는 통행구로 비활성화하고, 풍차와 숲 가장자리 구성은 보정 JSON에서 이격했다. 최종 병합 계획의 활성 배치는 131건이며 모두 유효한 `정적경관배치InstanceView`를 가진다.
- 이전 Scene 생성 방식이 남긴 정적 Synty 경관 중복 164개를 적용 성공 뒤 제거했다. 현재 창고 상호작용용 관측 Fixture 하나는 보존했으며, JSON 경관과 이전 경관이 이중으로 그려지지 않는다.
- 창고 상호작용 수직 단위를 다시 조립해 Target과 Controller 배선을 각각 유효 상태로 복구했다. 1인칭 화면의 기존 `Scene 배선 오류` 대신 실제 `대관령 감자 상자 Pallet · 현재 3개` 안내가 표시된다.
- 최종 집중 시험은 정적 경관 Pipeline 14/14, 창고 아이템 5/5, 통합 PlayMode 2/2가 통과했다. 앞선 회귀 시험인 통합 월드 3/3, 전투 입력 3/3, 턴 마감 5/5, 농장 경영 3/3도 통과했다.
- 최종 Game View는 `02-static-scenery-first-person-game-view.png`와 `03-static-scenery-farm-tactical-game-view.png`다. Unity 6000.5.6f1 DX12 Play Mode에서 1인칭과 전술 3인칭 버튼 전환을 직접 확인했다.
- 실행 중인 서버가 없어 턴 마감·입고 UI·농장 전투 세션 오류 3건은 남는다. 로컬 오디오 출력 장치 전환 중 FMOD 오류 1건도 관찰했으며 경관·카메라 검증과 분리해 기록한다.
- 실제 DEM·토지피복·수계 연결과 HLOD bake는 이번 범위가 아니다. commit·push·배포는 수행하지 않았다.

## 감자 작물과 밭고랑 정렬 보완

- `Dirt Rows` Prefab의 Renderer 중심이 root Pivot에서 치우친 값을 기본 계획 생성 단계에서 보정했다.
- 감자 작물은 전체 밭 외곽에 임의 산포하지 않고, 12개 밭고랑 타일마다 두 개씩 총 24개를 중앙부에 배치한다. 작물과 밭고랑의 회전은 모두 8도로 맞췄다.
- 계획 검증은 실제 Prefab Renderer 경계를 로컬 좌표로 측정한다. 작물 Renderer가 어느 밭고랑 Renderer에도 완전히 포함되지 않거나 방향 차이가 5도를 넘으면 각각 `CropOutsideSoilRowBounds`, `CropRowRotationMismatch` 오류로 Scene 적용을 막는다.
- 재생성·재승인·Scene 적용 뒤 활성 배치는 127건이다. 저장 Scene에서 밭고랑 14개, 감자 작물 24개, 경계 이탈 0개, 작물 회전 8도 단일값을 확인했다.
- 정적 경관 Pipeline EditMode는 15/15가 통과했다. 성능 합계는 삼각형 212,151/222,412, Material Slot·Draw Call 378/408, Shadow Caster 238/260, Collider 194/213, Animator 5/7이다.
- Unity 6000.5.6f1 DX12 Play Mode의 농장 전술 시점에서 각 밭고랑 중앙에 작물이 들어간 화면을 다시 확인했으며 최종 증거는 `04-crops-centered-in-field-game-view.png`다.
