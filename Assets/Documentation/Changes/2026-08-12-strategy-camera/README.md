# PLAYER-CAMERA-0 전략 카메라

`SimulationWorldShell`에 Play Mode Game View용 전략 카메라 이동·회전·확대/축소를 추가했다.

## 조작

- `WASD`: 현재 카메라 Y축 회전을 기준으로 지면 이동
- `Q/E`: Y축 연속 회전
- Mouse Wheel: 최소 12, 최대 110 거리 안에서 확대·축소
- Right Mouse Drag: Y축과 Pitch 자유 회전

이동 중심은 X -65~65, Z -50~50 범위에서 제한된다. 이동과 회전은 `Time.unscaledDeltaTime`을 사용하므로 프레임률과 Simulation 일시정지에 종속되지 않는다.

## 구조와 경계

```text
PlayerCameraRig
 ├─ CameraPivot
 │   └─ Main Camera
 └─ 전략카메라Controller
```

카메라는 Presentation 기능이다. 입력 중에도 `WorldTick`과 상태 버전을 바꾸지 않으며 서버 명령을 호출하지 않는다. `InputSystemUIInputModule`을 유지하고 `StandaloneInputModule`은 사용하지 않는다.

왼쪽 클릭 배치 객체 선택, `ESC` 초점 해제와 타로 카드 연동은 다음 단계다. Controller에는 자유 탐색과 배치 객체 초점 상태 경계만 먼저 마련했다.

## Game View

- [전략 카메라 정착지 구도](simulation-world-shell-strategy-camera.png): 정착지 화면에서 이동·자유 회전·Zoom을 적용한 1600×900 Play Mode 결과다.

## 검증

- 카메라 상태·경계·프레임 분할 독립성 EditMode: 6/6 통과
- `SimulationWorldShell` 저장 Scene 회귀: 11/11 통과
- Input System WASD·E·Mouse Wheel·Right Mouse Drag PlayMode: 1/1 통과
- 입력 전후 `WorldTick 12`, 상태 버전 12 유지
- 실제 서버 API는 호출하지 않음

현재 Console에는 카메라와 무관하게 로컬 예행연습 서버가 실행되지 않을 때 발생하는 기존 `TurnClosingServerRequestFailed:0:ConnectionError`가 남는다. 카메라 테스트 자체의 오류는 없다.
