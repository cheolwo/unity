# Synty 농장 플레이어 보행 표현

## 적용 범위

- 보유한 Polygon Farm·Town·City·Generic·Starter 팩의 캐릭터는 Humanoid 리그를 제공하지만 별도의 `.anim`, Animator Controller, FBX 내장 Animation Clip은 제공하지 않는 것을 확인했다.
- Synty 원본 Prefab·FBX·Avatar는 수정하지 않고 `공용AnimationAdapter`가 Humanoid 뼈에 표현 전용 자세를 적용한다.
- 1인칭 WASD 이동과 RTS형 우클릭 이동은 같은 Idle·Walk 상태를 사용한다.
- Shift+W 이동은 Run 상태로 전환하며 걷기보다 보폭과 재생 주기가 커진다.
- 팔·허벅지·종아리·척추·골반을 함께 움직이고 정지 시에는 작은 호흡 자세로 자연스럽게 복귀한다.
- 이동과 Animation은 `PresentationOnly`이며 서버 revision, WorldTick, 업무 완료 상태를 변경하지 않는다.

## Game View

![대관령면 Farm Synty 플레이어 보행](synty-farm-player-locomotion-game-view.png)

## 검증 기준

- `공용AnimationAdapter`는 Synty Humanoid Avatar를 재사용하고 Root Motion을 비활성화한다.
- 일반 이동은 `walk`, Shift 이동은 `run`, 정지는 `idle` 의도를 사용한다.
- 실제 위치 변경은 `플레이어경관Controller`가 담당하고 Animation은 뼈의 시각 자세만 변경한다.
- Game View 이미지는 실제 Play Mode에서 캐릭터가 이동하는 동안 저장한다.
