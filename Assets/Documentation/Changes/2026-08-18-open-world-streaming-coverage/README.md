# OPEN-WORLD-0 스트리밍 이동 범위 전환

`SimulationWorldShell`의 플레이어와 전술 카메라가 평창 Fixture의 고정 사각형에
묶이지 않고 현재 `공간TileStreamingController`가 추적하는 Tile 범위를 사용하도록
전환한 첫 오픈 월드 기반 기록이다.

## 플레이 변화

- Streaming이 준비된 저장 Scene에서는 1인칭·전술 이동 목적지와 플레이어 위치를
  기존 `X -30.5~30.5 / Z -22.5~22.5` 사각형으로 되밀지 않는다.
- 전술 카메라 초점은 현재 9×9 Prefetch Tile 범위 안에서 유지한다.
- 안전 이동은 계속 `추적 Tile + 지면 충돌`을 모두 요구한다.
- Streaming이 없는 실험 Scene은 기존 `플레이어경관Profile` 경계를 fallback으로 쓴다.
- 이동과 카메라는 표현 상태이며 `WorldTick`과 서버 개정을 바꾸지 않는다.

## 검증

- Unity 6000.5.6f1 스크립트 재컴파일: 오류 0건
- .NET `Ssalddel.Unity.Tests.EditMode.csproj` 빌드: 성공, 오류 0건
- .NET `Ssalddel.Unity.Tests.PlayMode.csproj` 빌드: 성공, 오류 0건
- `공간TileStreamingTests` EditMode: 10/10 통과
- `SimulationWorldShellTests` EditMode: 12/12 통과
- `통합월드자유이동PlayModeTests`: 2/2 통과
  - 실제 `W` 입력으로 기존 Farm 경계 `X 10.5` 통과
  - Streaming Coverage가 준비되면 기존 평창 최대값 `X 30.5` 밖의 위치를
    Profile 재적용이 되돌리지 않으며 `WorldTick`·개정은 그대로임

## 완료하지 않은 범위

- 이번 변경은 저장 Scene의 기존 Streaming·안전 이동 배선을 재사용하며 Scene 파일을
  새로 저장하지 않았다.
- 현재 Scene의 물리 지형 Collider는 기존 범위 밖 실제 보행을 아직 지원하지 않는다.
- H4 AreaSet에 따른 H3 Graph 준비·활성·캐시 해제 Coordinator, 실제 H2 Block,
  GraphRelation 양쪽 Gate와 외부 Tile 지형은 후속 단계다.
- Game View 수동 조작과 새 PNG 캡처는 수행하지 않았다. 따라서 이 기록은
  코드·시험·저장 Scene Play Mode 증거이며 완성된 Farm→Hub 오픈 월드 화면 증거가 아니다.
