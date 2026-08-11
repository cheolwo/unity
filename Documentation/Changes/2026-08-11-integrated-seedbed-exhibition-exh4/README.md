# 통합 모판·전시관 EXH-4 주문자 집단·도심마트

EXH-3 Scene에 Town 주문자 집단과 City 도심마트를 연결하는 다섯 번째 전시를 추가했다. 개인 의향, 개인정보 제거 집계, 주문자 공개상품, 마트 운영자 전용 재고·진열 작업은 같은 업무 계보를 보되 서로 다른 공개범위와 stable ID를 유지한다.

- `IndividualIntent`는 본인 비공개이며 철회 가능하다.
- `GroupingPreview`와 `OrdererGroupSummary`는 개인정보를 제거한 집계이고, Preview만으로 참여나 주문을 확정하지 않는다.
- `MartPublicProduct`의 판매 가능 수량은 주문자 공개용 projection이며 물리 후방재고가 아니다.
- `MarketInventory`와 `ShelfTask`는 마트 운영자 권한 전용이고 전시관은 운영 Command를 제공하지 않는다.
- 마트 판매가는 KAMIS 관측값과 비교할 수 있지만 KAMIS를 판매가 원천으로 사용했다고 주장하지 않는다.

Unity 6000.5.6f1 재컴파일 오류 0건, Scene Builder 검증, 집중 EditMode 5/5와 Play Mode Console 새 오류 0건을 확인했다. EXH-4 전시를 선택한 1600×900 Game View를 캡처했다.

![주문자 집단과 도심마트 공개범위 전시](integrated-seedbed-exhibition-exh4.png)
