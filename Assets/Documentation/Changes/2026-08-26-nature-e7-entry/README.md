# Nature E7 실제 진입과 도끼 확보

- canonical Scene: `SimulationWorldShell`
- 플레이어 입력: 하단 `Nature 탐험` 버튼, 중앙 조준선, 마우스 왼쪽 버튼
- 확인 상태: `WI-NATURE-05` 도끼 확보, 다음 벌목 선택 상태
- 권위 경계: Unity 카메라·HUD·Animation·Audio 표현은 Simulation 명령 결과를 표시하며 `WorldTick`을 직접 변경하지 않는다.
- 자동 증거: 전체 폐루프 PlayMode 1/1, WI-NATURE-05 PlayMode 1/1, 통합 모드·첫 대상 조준 1/1, Nature EditMode 12/12
- 화면 증거: 실제 Editor Play Mode에서 `Nature 탐험` 버튼과 도끼 획득 입력 후 Game View를 저장한다.
- 장면 무결성: 저장 canonical Scene Missing Script 0개. Test Runner의 10개 경고는 `Temp/__Backupscenes/0.backup` 임시 사본에서 발생한다.
- Console: 실제 입력·캡처 실행의 오류 0개. 남은 경고는 LocalProcess 미이관 능력 대기다.
- 제외: 승인 Nature Ambient·BGM, 실제 음향 청취, 사람이 도끼부터 저장 복원까지 전 과정을 수동 완주한 증거

`nature-e7-axe-acquired-game-view.png`는 도끼 획득 뒤 HUD의 `도끼 보유` 상태와 다음 벌목 선택 안내를 보여준다.
