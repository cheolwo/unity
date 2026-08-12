# TURN-CARD-UI-1B 턴 마감 카드

`SimulationWorldShell`의 경영 화면에 턴 마감 패널을 연결했다. 플레이어는 카드 없이 넘기거나 바보·전차 Fixture 카드 중 한 장을 고르고, Preview로 미완료 업무와 다음 턴 효과를 확인한 뒤 Confirm할 수 있다.

## 권위 경계

- Preview는 현재 `WorldTick`과 `Revision`을 바꾸지 않는다.
- Confirm 결과가 현재 session, revision, closing turn, next turn과 일치할 때만 WorldShell snapshot을 교체한다.
- 현재 실제 Unity Scene은 `턴마감FixtureAuthorityClient`를 사용한다. 카드 내용은 `evening-hakdang.fixture-r1`이며 승인 publication이나 운영 HTTP 응답이 아니다.

## Game View

- [바보 카드 Preview](turn-closing-fool-preview.png): Day 13, Tick 12, Revision 12를 유지하면서 미완료 업무 2건과 다음 턴 `Awareness +1`을 표시한다.
- [Confirm 후 다음 날](turn-closing-next-day.png): Day 14가 시작되고 HUD는 04-13, Tick 13, Revision 13으로 교체되며 `BeginnerMind`가 활성화된다.

## 검증

- `턴마감Tests`: 3/3 통과
- 전체 Unity EditMode: 212/213 통과. 실패 1건은 기존 연구 Scene 기대 개수 27과 현재 28의 불일치다.
- `TURN-CARD-UI-1B validation passed`
- Play Mode 골든 패스 실행 후 새 Console 오류 0건
