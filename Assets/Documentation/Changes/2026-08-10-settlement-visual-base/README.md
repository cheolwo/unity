# SETTLEMENT-VISUAL-BASE-0

`SimulationWorldShell`의 첫 정착지에 semantic `VisualKey`와 기존 Farm/Urban/Environment catalog를 이용한 1차 시각 기반을 적용했다.

## 적용 범위

- Farm: 10개 경작지, 감자 작물, Barn, Silo, 300kg HarvestLot 상자
- Town/Market: 주거·시장 건물과 생산물 판매대
- Storage/Logistics: 창고, pallet, cargo box, van
- Residential: 주거 건물과 수목
- Landscape: 언덕, 수목, 꽃과 기존 도로 골격
- Gate/Garrison: 후속 기능을 위한 primitive placeholder 유지
- 시간대: 오후 15:00 고정 Presentation. Simulation Tick/Revision은 변경하지 않음

Vendor prefab은 `WorldVisualInstanceView`와 catalog 뒤에 있으며 prefab 이름이나 경로는 Domain stable ID가 아니다. 상자와 Renderer 개수도 재고·Task 완료의 권위를 갖지 않는다.

## 검증

- `SimulationWorldShellTests`: 10/10 통과
- `Ssalddel.Unity.Tests.EditMode`: 57/57 통과
- `HarvestDispositionChoiceViewTests`: 4/4 통과
- Play Mode Overview/Farm/Market 전환 및 Game View 캡처
- 최종 Play Mode Console 오류: 0건

## Game View

- `settlement-overview.png`: 하나의 화면에서 Farm, Town, Market, Residential, Storage, Logistics, Gate/Garrison socket을 확인
- `farm-district.png`: 감자 경작지와 300kg HarvestLot focus
- `market-district.png`: 시장 건물과 두 생산물 판매대 focus

이 단계는 읽기 가능한 1차 시각 기반이다. 판로 Preview·Confirm·Task·Tick·Effect 연결은 다음 `SETTLEMENT-INTERACTION-0`에서 수행한다.
