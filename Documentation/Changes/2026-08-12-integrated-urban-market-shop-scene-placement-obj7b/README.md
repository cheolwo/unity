# OBJ-7B 도심마트 Shop Scene 배치

EXH-4의 `seedbed-object:city.urban-market-building.a`를 독립 Object 모판 O5에서
`SimulationWorldShell`의 마트 구역 O6 Scene 배치로 승격했다.

## 배치와 공개 경계

- placement stable ID: `scene-placement:simulation-world-shell.market.urban-market-shop.a`
- zone stable ID: `district:market`
- profile: `r1`
- anchor: `market.public-products.shop`
- binding: `MartPublicProduct:mart-product:sim.potato.public`
- socket: `Entry`, `PublicProduct`, `DemandSignal`

이 Shop은 공개 상품과 개인정보 제거 수요 신호를 표현한다. `MarketInventory`를 binding이나
socket으로 노출하지 않으며, 재고 변경·주문 확정·운영 실행의 권위는 서버에 남는다.

## Runtime 증거

`integrated-urban-market-shop-scene-placement-obj7b.png`은 2026-08-12에
`SimulationWorldShell` Play Mode에서 마트 구역을 선택한 뒤 1600x900 Game View로 캡처했다.
도심마트 건물, 공개 진열대, 구역 표지와 이동 UI가 한 화면에서 확인된다.

Play Mode 콘솔에는 서버 미연결 환경의 기존
`TurnClosingServerRequestFailed:0:ConnectionError` 1건이 있었으며, 이번 Object 배치에서
새로 발생한 오류는 확인되지 않았다.
