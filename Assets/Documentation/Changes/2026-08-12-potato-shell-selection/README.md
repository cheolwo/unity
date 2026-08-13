# POTATO-SHELL-1 감자 객체 선택과 초점

`SimulationWorldShell`에서 기존 감자 수확 Lot과 감자 운송 화물을 공통 입력 흐름으로 선택할 수 있게 했다.

## 조작

- 왼쪽 클릭: 객체를 선택하고 연결된 정보·행동 패널을 연다. 카메라는 즉시 이동하지 않는다.
- `F`: 현재 선택 객체의 카메라 기준점으로 초점을 옮긴다.
- `Esc`: 객체 선택을 해제하고 상위 구역으로 돌아간다.
- `WASD`, `Q/E`, Mouse Wheel, Right Mouse Drag: 기존 자유 탐색을 유지한다.

선택 안내는 한국어 객체 이름, 고유 식별자 계보, `F`·`Esc` 조작과 화면 강조의 한계를 함께 표시한다. 감자 수확 Lot은 기존 판로 Preview/Confirm 패널, 감자 운송 화물은 기존 배차 Preview/Confirm 패널을 재사용한다.

## 경계

클릭·초점·해제는 Presentation 선택 상태만 바꾸며 `WorldTick`과 상태 버전을 바꾸지 않는다. Preview는 후보를 보여줄 뿐이고, Confirm 뒤 서버 기준 상태를 다시 읽는 기존 경계를 유지한다. 화면 강조나 차량 표현은 생산·운송·입고·재고 완료의 근거가 아니다.

## Game View

- [감자 수확 Lot 객체 초점](potato-shell-object-focus.png): 왼쪽 아래 공통 선택 안내와 오른쪽 수확 Lot 행동 패널을 함께 보여주는 1600×900 Play Mode 결과다.

## 검증

- Unity Editor 스크립트 컴파일 오류 0건
- `SimulationWorldShellTests` 12/12 본문 실행 통과
- Editor 내부 선택→`F` 초점→`Esc` 해제 검증 통과
- Play Mode Input System `F` 초점과 `Esc` 해제 검증 통과
- 입력 전후 `WorldTick 12`, 상태 버전 12 유지
- 테스트 수집기 API는 `Temp/pipeline_test_status.json` 파일 잠금 때문에 새 결과 수집이 차단되어, 같은 시험 본문 직접 실행과 Play Mode 입력 검증으로 대체했다.
- Play Mode Console의 유일한 오류는 로컬 Simulation 서버 미실행에 따른 기존 `TurnClosingServerRequestFailed:0:ConnectionError`이며 객체 선택·카메라 오류는 없었다.

실제 운영 API, 주문, 결제, 기사 호출, 재고 쓰기와 외부 제공자 호출은 수행하지 않았다.
