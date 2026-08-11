# ART0~ART3 세 Region·Hub 1차 미술 패스

- 화면: `ThreeRegionHubJourney` 1600×900 카메라 렌더
- ART0 방향: 따뜻한 지역사회, 절제된 로우폴리, 데이터가 움직이는 작은 세계
- ART1 구성: Farm·Town·Hub·City의 독립 색면, 연속 도로 5개, 농장 식생·Town 주택·City 상점/오피스·Hub 물류 소품 배치
- ART2 색: Farm의 초록/흙색, Town의 올리브, Hub의 중성 산업색, City의 회청색으로 영역을 구분하되 공통 저채도 범위 유지
- ART3 조명: 따뜻한 단일 태양, soft shadow, Trilight ambient와 고정 top-down/2.5D 카메라 적용
- 데이터 표현: 사람·화물 경로는 `DataRoute_*`의 가는 리본으로 제한하고, overview에서 읽히지 않는 World Text는 숨김
- 권위 경계: 환경 prefab, 도로, 조명, 카메라와 경로 리본은 Presentation이며 업무 stable ID·상태 확정 권위를 갖지 않음
- 검증: 전용 EditMode 10/10 통과, Unity 카메라 직접 렌더 PNG 확인
- 한계: 1차 기준선이라 City 건물 변형과 Town 보행 공간, 경계 지형 전환, AO/Color Grading, NPC·차량 생활감은 후속 ART4~ART7 대상

기존 CMP5 캡처: [three-region-hub-journey-playmode.png](../2026-08-10-three-region-hub-journey/three-region-hub-journey-playmode.png)

![ART0~ART3 1차 미술 패스](three-region-hub-art-first-pass.png)
