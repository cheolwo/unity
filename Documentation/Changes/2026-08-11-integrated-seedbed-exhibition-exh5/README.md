# 통합 모판·전시관 EXH-5 음식배달 인계

EXH-4 Scene에 음식 주문, 음식점 준비, 기사 제안·수락, 픽업·전달, 주문자 수령 확인을 잇는 여섯 번째 전시를 추가했다. 음식배달은 화물운송의 `CargoJourney`나 창고 인계 상태를 재사용하지 않고 별도의 8단계 계보와 stable ID를 유지한다.

- 기사 후보에게는 수락 전 대략적인 전달 권역만 보이며 수령인 상세정보는 노출하지 않는다.
- 기사 본인 수락 뒤에만 확정 기사 권한으로 픽업·전달 단계가 이어진다.
- `전달완료`와 주문자의 `수령확인`은 서로 다른 canonical record와 별도 Confirm이다.
- 전시관은 `SimulationPreview`까지만 제공하며 음식주문·배차·완료 운영 Command를 실행하지 않는다.
- 운영 음식배달 snapshot은 아직 연결하지 않았고 Fixture 상태를 명시한다.

Unity 6000.5.6f1 재컴파일 오류 0건, Scene Builder 검증, 집중 EditMode 6/6와 Play Mode Console 오류 0건을 확인했다. EXH-5 음식배달 전시를 선택한 1600×900 Game View를 캡처했다.

![음식점 기사 주문자 인계 전시](integrated-seedbed-exhibition-exh5.png)
