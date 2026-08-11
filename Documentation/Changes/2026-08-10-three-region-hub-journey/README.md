# CMP5 Farm·Town·City·지역 물류허브 Journey

- 화면: `ThreeRegionHubJourney` Play Mode Overview
- 독립 영역: Farm, Town, City, Regional Logistics Hub
- CMP3 Gate: passenger 4쌍의 connector와 freight 3쌍의 connector를 가진 Gate prefab 10개
- 사람 Journey: Farm↔Town, Town↔City 2개; freight yard 남쪽 corridor 사용
- Farm 화물: 기존 `cargo:transport-71`과 WORLD-4 lineage 6개 재사용, Hub 보관에서 정지
- Town sample 화물: `outbound-allocation:town-delivery-01.city-01` 근거가 있을 때만 City outbound 차량 활성
- 권위 경계: 사람·차량 위치나 animation 완료는 입고·검수·보관·출고·판매 상태를 변경하지 않음
- 검증: 전용 EditMode 7/7, 전체 EditMode 101/101, Play Mode Game View 확인
- 한계: A형 관통 구조 기준선이며 최종 경관 밀도·도로 연속 배치·카메라 품질 단계는 CMP7~CMP11에서 보강

![세 Region과 지역 물류허브 Journey](three-region-hub-journey-playmode.png)
