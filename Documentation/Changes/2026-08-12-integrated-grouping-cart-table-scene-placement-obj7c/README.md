# OBJ-7C 집단수요 Cart Table Scene 배치

EXH-4의 `seedbed-object:town.grouping-cart-table.a`를 독립 Object 모판 O5에서
`SimulationWorldShell`의 Town 구역 O6 Scene 배치로 승격했다.

## 배치와 개인정보 경계

- placement stable ID: `scene-placement:simulation-world-shell.town.grouping-cart-table.a`
- zone stable ID: `district:town`
- profile: `r1`
- anchor: `town.orderer-group.grouping-cart-table`
- binding: `GroupingPreview:grouping-preview:sim.potato.town`
- socket: `IntentInput`, `AggregateOutput`, `ConsentBoundary`

이 Cart Table은 개인정보를 제거한 집단화 Preview와 공개 집계의 표현이다. 개인 의향 원문,
자동 참여, 주문 확정, 운영 Command를 소유하지 않으며 실제 참여에는 별도 명시적 동의가 필요하다.

## Runtime 증거

`integrated-grouping-cart-table-scene-placement-obj7c.png`은 2026-08-12에
`SimulationWorldShell` Play Mode에서 Town 구역을 선택한 뒤 1600x900 Game View로 캡처했다.
Town 구역의 Cart Table, 주변 건물, 구역 표지와 이동 UI가 한 화면에서 확인된다.

Play Mode 콘솔에는 서버 미연결 환경의 기존
`TurnClosingServerRequestFailed:0:ConnectionError` 1건이 있었으며, 이번 Object 배치에서
새로 발생한 오류는 확인되지 않았다.
