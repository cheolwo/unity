# OBJ-6B 물류 Object 모판 분해

## 결과

- EXH-3의 `화물 배송 차량`, `공용 화물 Pallet`, `농장 출하 Pallet Crate`를 독립 wrapper prefab으로 분리했다.
- 세 Object는 각각 `town.delivery-truck.a`, `shared.cargo-pallet.a`, `farm.pallet-crate.a` semantic Visual Variant를 사용한다.
- 배송 차량은 Cargo Journey와 운송 Task, pallet은 Hub Receiving과 Warehouse Handoff, 농장 crate는 Harvest Cargo와 Hub Receiving의 연결 후보를 보존한다.
- prefab과 Scene 위치로 배차·화물 상태·입고·재고를 추정하지 않는다.
- 통합 Object 모판은 기존 일곱 Object와 새 세 Object, 총 10개를 한 번에 선택·Preview한다.
- 새 세 Object는 O5 `RuntimeVerified`이며 대상 Scene placement가 없어 O6는 차단한다.

## 검증

- Unity 재컴파일: 오류 0건
- `통합전시관Object모판Tests`: 4/4 통과
- Play Mode Console: 오류 0건
- 화물 배송 차량을 기본 선택해 1600×900 Game View를 확인했다.

## 화면

![화물 배송 차량을 선택한 통합 Object 모판](integrated-logistics-object-seedbed-obj6b.png)

이 화면은 물류 업무를 실행하는 화면이 아니라, Scene에 심기 전 개별 Object의 의미·크기·소켓·binding 경계를 검증하는 O5 Preview다.
