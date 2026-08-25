# Nature E7 실제 진입과 도끼 확보

- canonical Scene: `SimulationWorldShell`
- 플레이어 입력: 하단 `Nature 탐험` 버튼, 중앙 조준선, 마우스 왼쪽 버튼
- 확인 상태: `WI-NATURE-05` 도끼 확보, 다음 벌목 선택 상태
- 권위 경계: Unity 카메라·HUD·Animation·Audio 표현은 Simulation 명령 결과를 표시하며 `WorldTick`을 직접 변경하지 않는다.
- 입력 경계: 일인칭 커서를 푼 뒤 하단 모드 UI를 누를 때 상호작용이나 커서 재잠금으로 소비하지 않도록 Unity UI 레이캐스트와 지속 모드 영역을 먼저 판정한다.
- 자동 증거: 전체 폐루프·감각 표현 결합 PlayMode 2/2(26.05초), 입력 경계 EditMode 1/1, WI-NATURE-05 PlayMode 1/1, 통합 모드·첫 대상 조준 1/1, Nature EditMode 12/12
- 화면 증거: 실제 Editor Play Mode에서 `Nature 탐험` 버튼과 도끼 획득 입력 후 Game View를 저장한다.
- 장면 무결성: 저장 canonical Scene Missing Script 0개. Test Runner의 10개 경고는 `Temp/__Backupscenes/0.backup` 임시 사본에서 발생한다.
- Console: 실제 입력·캡처 실행의 오류 0개. 남은 경고는 LocalProcess 미이관 능력 대기다.
- 음향 경계: 48 kHz Stereo, Player Listener 활성과 절차형 행동 효과음 4개 결속을 확인했다. 프로젝트의 승인 음원 파일은 0개이며 Ambient·BGM은 선택 채널 대기, Editor는 FMOD 출력 장치 초기화 오류 60으로 실제 청취 불가다.
- 제외: 승인 Nature Ambient·BGM, 실제 음향 청취, 사람이 도끼부터 저장 복원까지 전 과정을 수동 완주한 증거

`nature-e7-axe-acquired-game-view.png`는 도끼 획득 뒤 HUD의 `도끼 보유` 상태와 다음 벌목 선택 안내를 보여준다.
