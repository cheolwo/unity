# COOP-1 생산자 조합 출하 인수

## 결과

- `CooperativeShipment`으로 결정된 300kg HarvestLot만 조합 인수 검토를 시작할 수 있다.
- Preview·Confirm·Simulation Tick 뒤 300kg 조합 인수 Lot과 `PotatoHarvestCargoLifecycle` 후속 후보를 만든다.
- `CARGO-1 열기`는 같은 HarvestLot과 조합 인수 lineage를 가진 포장 검토 snapshot만 만든다.
- PackageLot, Cargo, 조합 정산, 실제 포장·상차·운송은 생성하지 않는다.

## 대표 화면

![조합 인수와 CARGO-1 포장 검토 준비](cooperative-intake-game-view.png)

## 검증

- COOP-1 core 집중: 8/8 통과
- Unity core 전체: 313/313 통과
- Unity EditMode focused: 4/4 통과
- Play Mode Game View: 직접 확인
