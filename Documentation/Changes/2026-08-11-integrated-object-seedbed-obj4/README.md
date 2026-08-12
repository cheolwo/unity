# OBJ-4 Farm·자료관·모판 Object 분해

## 결과

- 기존 세 Object에 농장 온실, 감자 밭고랑, 감자 재배체, 밭 관수 스프링클러를 추가해 총 일곱 wrapper prefab을 구성했다.
- `통합전시관ObjectVisualCatalog`와 Preview 버튼은 고정 수량이 아니라 catalog 항목 수를 따른다.
- 자료관 시청은 Backdrop, 관측 구체는 Marker, 모판 연구대는 Preview 전용 가구로 유지해 업무 Object로 잘못 승격하지 않았다.
- 각 wrapper는 semantic stable ID, footprint, 실측 bounds와 required socket Transform을 가진다.
- Preview는 항상 Object 하나만 표시하며 운영 Command를 제공하지 않는다.

## 검증

- Unity 6000.5.6f1 재컴파일: 오류 0건
- 전용 EditMode: 3/3 통과
- Play Mode Console: 오류 0건
- Game View: `integrated-object-seedbed-obj4.png` 1600×900

## 경계

- 일곱 Object는 O5 `RuntimeVerified`다.
- `SimulationWorldShell` 대상 Scene placement와 O6 receipt는 만들지 않았다.
- 실제 농장 상태, 관수 명령, 공공데이터 호출, 주문·배차·창고 Command를 실행하지 않았다.
