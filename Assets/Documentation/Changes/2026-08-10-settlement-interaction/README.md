# SETTLEMENT-INTERACTION-0

`SimulationWorldShell`의 감자 300kg HarvestLot 선택에서 네 판로의 Preview·Confirm·Task 예약·WorldTick·Effect·새 snapshot reconcile을 한 화면으로 연결했다.

## 흐름

```text
HarvestLot 선택
  → 생산자 조합 출하 / 온라인 직접 판매 / 비축 보관 / 외부 교역 준비
  → Simulation authority Preview
  → 명시적 Confirm
  → allocation 및 capacity 예약
  → 명시적 World Tick
  → Effect 적용
  → authoritative snapshot으로 HUD와 카드 갱신
```

Production seam은 공식 Simulation API의 다음 경로를 사용한다.

- `GET api/simulation/v1/sessions/{sessionStableId}`
- `POST .../harvest-disposition-impact-previews`
- `POST .../harvest-disposition-impacts/confirm`
- `POST .../ticks`

Game View는 실제 서버가 아닌 명시적인 `SimulationFixtureAuthority` test double로 검증했다. fixture도 Preview에서 snapshot을 바꾸지 않고, Confirm에서만 노동·재정·storage capacity를 예약하며, 완료 Tick에서만 경제 Effect를 적용한다. 실제 판매·배송·수출·정산은 실행하지 않는다.

## 비축 경로 증거

- Preview: revision 12, WorldTick 12 유지, 비용 15,000 KRW·노동 6·기간 1 Tick·예상 비축 294kg·FoodSecurityDays 10→12.94 후보
- Confirm: revision 13, WorldTick 12, allocation Reserved와 Task Scheduled
- Tick 완료: revision 14, WorldTick 13, 재정 985,000 KRW·storage 1,494kg·FoodEquivalent 1,552.8·FoodSecurityDays 12.94, Effect Applied

## 검증

- `SettlementInteractionTests`: 8/8 통과
- `Ssalddel.Unity.Tests.EditMode`: 65/65 통과
- Preview와 Effect Applied 1600×900 Play Mode Game View
- 최종 Play Mode Console 오류: 0건

## 캡처

- `reserve-preview.png`
- `reserve-effect-applied.png`
