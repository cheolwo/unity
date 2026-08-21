# Play Mode 최소 Player 화면

- 공식 Scene: `SimulationWorldShell`
- 표현 정책: Play Mode에서 Player 시각 모델 하나와 하단 `전략 시점 / 1인칭 / 3인칭` 전환만 표시
- 보존 범위: 서버·시뮬레이션·공간 오브젝트는 삭제하지 않고 Renderer·Terrain·보조 Canvas만 표현에서 숨김
- 기본 시점: Player가 보이는 3인칭
- 동적 표현: Play 시작 뒤 늦게 생성되는 fixture·공간 표식도 같은 정책으로 숨김

## 검증

- Unity C# 컴파일: 오류 0개
- 열린 Scene `WORLD-UI-MINIMAL-1` 구조 검증: 통과
- 실제 Play Mode·Game View: Player 1명과 시점 전환 3개만 표시됨을 확인
- 증거: [minimal-player-game-view.png](minimal-player-game-view.png)

현재 로컬 Simulation 서버는 실행 중이 아니므로 서버 연결 요청은 `ConnectionError`로 남는다. 이 자료는 표현 정리와 시점 전환의 실행 검증이며, 서버 플레이 폐루프 완주 증거가 아니다.
