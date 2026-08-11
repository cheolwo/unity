# DIRECT-1 생산자 온라인 직접 판매 준비

## 결과

- `DirectOnlineSale`로 결정된 300kg HarvestLot만 생산자 소포장 검토를 시작할 수 있다.
- Preview·Confirm·Simulation Tick 뒤 Fixture 기준 5kg 소포장 60개와 상품 등록 후보를 만든다.
- 등록 초안은 비공개이며 가격 미설정, 주문 0이다.
- 상품 공개·주문·결제·택배 접수는 생성하지 않는다.

## 대표 화면

![생산자 소포장과 비공개 등록 초안](direct-online-sale-game-view.png)

## 검증

- DIRECT-1 core 집중: 8/8 통과
- Unity core 전체: 321/321 통과
- Unity EditMode focused: 4/4 통과
- Play Mode Game View: 직접 확인
