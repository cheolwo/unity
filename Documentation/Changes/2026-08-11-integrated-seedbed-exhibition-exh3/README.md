# 통합 모판·전시관 EXH-3 화물·Hub·창고

EXH-2의 자료관·Farm·모판 Scene에 화물차, Hub 창고, 입고 pallet과 일곱 checkpoint를 추가했다. 새 전시는 화주 의뢰 후보부터 Cargo Journey, Hub 도착·검수, 창고 인수까지 하나의 Fixture Cargo stable ID로 읽는다.

- `shipper request candidate → Cargo → Cargo Journey → Hub Receiving → Warehouse Handoff → Warehouse World Snapshot` 관계를 보존한다.
- 각 관계는 target revision과 별도의 expected target revision을 가진다.
- checkpoint는 `Candidate → Loaded → InTransit → ArrivedAtHub → Inspection → ArrivedAtWarehouse → ReceivingCompleted` 순서다.
- `Loaded`, `Inspection`, `ArrivedAtWarehouse`는 별도 Confirm이 필요한 경계로 표시한다.
- Hub 도착은 입고 완료가 아니고, 창고 도착은 검수·보관 완료가 아니다.
- 전시관은 운영 Command를 노출하지 않으며 현재 운영 Cargo snapshot은 불러오지 않았다.

Unity 6000.5.6f1 재컴파일 오류 0건, Scene Builder 검증, 집중 EditMode 4/4와 Play Mode Console 새 오류 0건을 확인했다. 화물 계보를 선택한 1600×900 Game View를 캡처했다.

![화물 Hub 창고 Cargo lineage 전시](integrated-seedbed-exhibition-exh3.png)
