# SimulationWorldShell 통합

농장 1인칭·RTS형 전술 시점과 진부면 입고 정보판을 별도 최종 Scene으로 늘리지 않고 기존 `SimulationWorldShell` 하나에 통합했다.

![대관령 농장 전술 화면](unified-world-farm-tactical-game-view.png)

![진부면 입고 정보판](unified-world-farm-hub-game-view.png)

## 화면에서 확인할 수 있는 것

- 화면 위쪽의 `월드`, `농장 1인칭`, `농장 전술`, `진부 입고` 버튼은 카메라가 바뀌어도 유지되는 별도 Overlay Canvas에 있다.
- 농장 전술 화면은 기존 Synty 농장 경관, 캐릭터 선택, 우클릭 이동과 전술 카메라를 그대로 사용한다.
- 진부 입고 화면은 같은 World와 상태 사본 위에서 창고 입고·검수·적재 흐름을 보여준다.
- 화면 전환은 표현 전용이며 `WorldTick`, 상태 개정 번호, 업무 완료를 만들거나 변경하지 않는다.

## 실행과 검증

- Build Settings에서 활성화된 Scene은 `Assets/Ssalddel/Scenes/SimulationWorldShell.unity` 하나다.
- EditMode 집중 시험 3개와 저장 Scene의 실제 버튼을 누르는 PlayMode 시험 1개가 통과했다.
- 두 PNG는 Unity Editor의 실제 Play Mode Game View에서 저장했다.
- 기존 연구 Scene과 `WorldBootstrapScene`은 삭제하지 않고 실행 진입점에서만 비활성화했다.
- Synty 원본 Prefab에서 발생하던 기존 누락 스크립트 경고는 이번 통합 범위에서 수정하지 않았다.
