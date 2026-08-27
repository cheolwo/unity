# Nature E7 실제 입력 폐루프와 저장 복원

- canonical Scene: `SimulationWorldShell`
- 플레이어 입력: 하단 `Nature 탐험` 버튼, 중앙 조준선, 마우스 왼쪽 버튼
- 확인 상태: `WI-NATURE-05` 도끼 확보 → 취소·재시도 → 나무 3그루 벌목 → 오두막 도면 배치·건설 → 입실·퇴실 → 저장 → Play Mode 재진입 복원
- 권위 경계: Unity 카메라·HUD·Animation·Audio 표현은 Simulation 명령 결과를 표시하며 `WorldTick`을 직접 변경하지 않는다.
- 입력 경계: 일인칭 커서를 푼 뒤 하단 모드 UI를 누를 때 상호작용이나 커서 재잠금으로 소비하지 않도록 Unity UI 레이캐스트와 지속 모드 영역을 먼저 판정한다.
- 자동 증거: 전체 폐루프 PlayMode 1/1(27.49초), 감각 표현 PlayMode 1/1(2.92초), 입력 경계 EditMode 1/1, WI-NATURE-05 PlayMode 1/1, 통합 모드·첫 대상 조준 1/1, Nature EditMode 12/12
- 화면 증거: 실제 Editor Play Mode에서 전체 폐루프를 수동 완주한 뒤 저장하고, Play Mode 재진입으로 `Day 2`, `도끼 보유`, `통나무 소지 0`, 그루터기 3개와 `오두막 완성`을 복원한 Game View다.
- 접근 회귀: 오두막 접근은 남은 나무보다 생활 거점을 우선하며, 이미 너무 가까우면 외벽 밖 안전 반경까지 물러난 뒤 완료한다.
- 장면 무결성: 저장 canonical Scene Missing Script 0개. Test Runner의 10개 경고는 `Temp/__Backupscenes/0.backup` 임시 사본에서 발생한다.
- Console: 실제 입력·캡처 실행의 오류 0개. 남은 경고는 LocalProcess 미이관 능력 대기다.
- 음향 경계: 48 kHz Stereo, Player Listener 활성과 절차형 행동 효과음 4개 결속을 확인했다. 프로젝트의 승인 음원 파일은 0개이며 Ambient·BGM은 선택 채널 대기, Editor는 FMOD 출력 장치 초기화 오류 60으로 실제 청취 불가다.
- E7 판정: 실제 입력·저장 Scene·Play Mode·Game View·Console·성공/취소/회복/귀환·결정적 Save/Restore/Replay와 최신 LocalProcess·RemoteHost 전체 동등성이 유효해 `PlayClosed`다.
- 선택형 증거: 현재 연결된 Windows 재생 종단점이 없어 실제 음향 청취는 수행하지 못했다. 승인 Nature Ambient·BGM과 함께 후속 감각 수용 항목으로 대기한다.

## 황혼 위협 관찰 추가 검증

- `WI-NATURE-01`은 실제 황혼 조우를 읽고 승인 Actual E5 관찰 공간에서 `O` Confirm을 수행한 뒤에만 `F` 전투와 `R` 후퇴를 연다.
- 기존 직접 실행 엔진은 선택형 관찰 계약을 구현하지 않아 구판 시험과 소비 경로를 유지한다.
- 자동 증거: Local Runtime EditMode `8/8`, canonical Scene 저장·복원·황혼 관찰 실제 입력 PlayMode `1/1`(30.39초), Core Nature·지역 사건 회귀 `58/58`.
- 화면 증거 수준: PlayMode에서 `nature-twilight-wi01-observed-game-view.png`를 로컬 검증 산출물로 캡처해 `위협 관찰 완료`, 압력 2 `Threatened`, 전방 위협·후방 안전 거점과 F/R 선택 표시를 확인했다. 재실행 직후 Console 오류는 0개다. 기존 안전 귀환 PNG를 황혼 관찰 화면 증거로 재사용하지 않는다.

## 명시적 장착·LH 표현 재검증

- `WI-ACTOR-02`는 실제 Input System `I` 입력과 Inventory·MainHand 버튼을 거쳐 장착·해제되며, 미장착 상태에서는 벌목을 차단하고 재장착 뒤에만 작업을 허용한다.
- H1마다 중복된 전역 Mountain Skybox Renderer를 끄고, 단일 Player View가 Play 중 `공간LHStreamingEngine`이 만든 나무·오두막·실내 배치를 숨기지 않도록 승인 경계를 보완했다.
- 자동 증거: `Nature생존E7폐루프PlayModeTests 6/6`, Nature 감각 표현 `1/1`, H 계층 실증 EditMode `2/2`. 보관 표현은 고정 이름이 아니라 실제 `BuildingPlacementStableId`의 실내 Overlay를 검증한다.
- 사람 직접 Game View에서는 Nature 전환과 도끼 획득·HUD 권위 전이를 확인했다. 자동화 화상 키보드가 자기 창에 입력을 소비해 사람 직접 `I` 장착 반복은 별도 수용 증거로 남긴다.

`nature-e7-restored-closed-loop-game-view.png`는 수동 완주 저장본을 재진입 복원한 안전한 다음 선택 상태를 보여준다.
