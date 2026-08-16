---
guideSchemaVersion: 1
guideStableId: landscape-guide:polygon-city-logistics
guideRevision: polygon-city-logistics-guide.v2
sourcePackCode: city
visualCatalogRevision: legal-dong-scenic-catalog.v2
compositionCatalogRevision: pyeongchang-four-pack-composition.v2
presentationOnly: true
---

# PolygonCity 도시·물류 경관 배치 기준

## 팩의 특징

`PolygonCity`는 평창을 대도시로 바꾸는 용도가 아니라 진부 Hub의 물류 진입, 상하차, 포장도로와 안전 설비를 선명하게 표현하는 보조 팩이다. 평창읍에서는 건물 밀도·층수 근거가 있는 경우에만 제한적으로 사용한다.

- Station과 포장도로는 Hub의 큰 실루엣과 차량 접근 방향을 만든다.
- Pallet·화물 상자·안전 설비는 적재와 대기 공간의 가장자리에 둔다.
- 회차 공간은 오브젝트로 채우지 않고 비워서 읽히게 한다.
- 상업 건물은 Town–Hub 전환부의 제한된 강조 요소로만 사용한다.

## 배치 순서

1. Station과 진입 도로로 Hub의 방향과 큰 덩어리를 정한다.
2. 상하차 Dock과 화물 대기 야드를 차량 동선 양쪽에 배치한다.
3. 회차 공간과 비상 통로를 비워 둔다.
4. 안전·서비스 설비로 적재 공간의 경계를 표시한다.
5. 작은 화물·표지·차량은 Task 거리에서만 추가한다.

`A`, `B`, `C`는 같은 물류 의미의 결정적 시각 변형이며 처리량이나 업무 상태를 뜻하지 않는다.

## 구성 세트 기준

| 구성 세트 | 주요 역할 | 허용 토지피복 | 물리 경사 | 필수 여유 공간 |
| --- | --- | --- | ---: | --- |
| 물류 Station 진입부 | Hub 진입 | 물류지 | 0~10도 | 차량·보행 진입 |
| 상하차 Dock | 화물 인계 | 물류지 | 0~10도 | 적재·차량 정렬 |
| 화물 대기 야드 | 화물 대기 | 물류지 | 0~10도 | 팔레트·회수 동선 |
| 포장도로·회차 공간 | 차량 회차 | 물류지·회랑 | 0~10도 | 회전 반경·비상 통로 |
| 안전·서비스 설비 | 작업 경계 | 물류지 | 0~10도 | 설비 접근 |
| Town–Hub 전환 경관 | 밀도 전환 | 주거지·물류지·회랑 | 0~10도 | 보행·차량 분리 |

## LOD와 성능

- Overview는 Station과 도로 실루엣만 유지한다.
- Region은 Dock·야드·회차 공간을 기능 덩어리로 보여준다.
- Task는 Pallet·상자·표지·안전 설비를 추가한다.
- 빈 회차 공간을 작은 화물 소품으로 채우지 않는다.

## Simulation과 상호작용

- 화물차·기사·화물은 Simulation 상태 사본을 따라 Socket에 별도로 표현한다.
- 정적 Van·Pallet·Box는 실제 입고·출고·운송 상태를 확정하지 않는다.
- Station 출입구, 차량 연결 지점, 화물 Socket과 회전 반경을 검증한다.

## 금지 요소

- City 자산으로 평창읍과 진부면에 근거 없는 고층 Skyline을 만들지 않는다.
- 화물 소품으로 회차·적재·비상 통로를 막지 않는다.
- 정적 차량과 화물을 Simulation 업무 완료로 해석하지 않는다.
- 실제 수계나 과도한 경사 위에 Station·도로·야드를 배치하지 않는다.

## 검토 체크리스트

- [ ] Station, 야드, 회차 공간과 안전 경계가 한눈에 구분된다.
- [ ] 차량 연결 지점과 회전 반경이 확보됐다.
- [ ] Town과 City의 밀도 차이가 평창 규모에 맞는다.
- [ ] 전체 Footprint와 화물 여유 공간이 로컬 제작 경계 안에 있다.
- [ ] 정적 경관과 Simulation 물류 상태가 분리돼 있다.
