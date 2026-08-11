# HARVEST-CHOICE-1 수확물 판로 선택

## 결과

- FARM-3에서 생성된 `harvest-lot:sim.potato.20260407.r1` 300kg 수확물과 상호작용하면 판로 카드가 열린다.
- 생산자 조합 출하, 온라인 마켓 직접 판매, 수출대행 준비 중 하나를 Preview하고 명시적으로 Confirm한 뒤 Simulation Tick으로 결정한다.
- 각 결정은 `CooperativeIntakeCandidate`, `ProducerPackingCandidate`, `ExportReadinessCandidate` 중 하나의 후속 업무 후보만 만든다.
- 상품 등록, 주문, 결제, 택배, 조합 인수, 수출계약, 검사, 통관, 실제 운송은 이 단계에서 실행하지 않는다.

## 조작

1. 수확물 마커를 클릭해 카드를 연다.
2. 세 판로 중 하나를 선택한다.
3. `확인`으로 command를 만든다.
4. `TICK`으로 판로 결정 원장을 반영한다.

## 대표 화면

![온라인 직판 결정 후 Game View](harvest-disposition-choice-game-view.png)

대표 화면은 Play Mode에서 온라인 직판 경로를 결정해 `ProducerPackingCandidate`가 생성된 상태다.

## 검증

- Core focused: 8/8 통과
- Unity EditMode focused: 4/4 통과
- Unity core 전체: 305/305 통과
- Play Mode Game View: 직접 확인
