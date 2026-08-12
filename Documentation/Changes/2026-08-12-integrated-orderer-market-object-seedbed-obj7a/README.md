# OBJ-7A 주문자 집단·도심마트 Object 모판

EXH-4의 주민 관점, 집단수요 Cart Table, 도심마트 Shop, 운영자 전용 재고 Shelf,
마트 운영자 Visual을 서로 다른 stable ID·wrapper prefab·placement profile·socket을 가진
O5 `RuntimeVerified` Object로 등록했다.

## 권위와 공개범위

- 주민 Visual은 실제 사람 identity나 의향을 소유하지 않는다.
- Cart Table은 집단화 Preview와 개인정보 제거 집계를 표시하지만 참여를 확정하지 않는다.
- 도심마트 Shop은 주문자 공개상품과 수요 신호만 표시하며 후방 재고를 공개하지 않는다.
- 운영자 Shelf는 권한 있는 재고·진열 Task용 Visual이며 공개 상품 수량과 분리된다.
- 운영자 Visual은 권한을 소유하지 않고 허가된 Perspective만 표현한다.

다섯 Object 모두 현재 독립 모판 O5 Preview까지만 통과했으며 대상 업무 Scene의 O6 배치는 아니다.

## Runtime 증거

`integrated-orderer-market-object-seedbed-obj7a.png`은 2026-08-12에
`통합Object모판` Play Mode에서 1600x900 Game View로 캡처했다. 선택된 도심마트 Shop과
15개 Object의 2열 선택 UI, semantic key, profile, footprint, bounds와 socket을 확인했다.
Play Mode 콘솔 오류는 0건이었다.
