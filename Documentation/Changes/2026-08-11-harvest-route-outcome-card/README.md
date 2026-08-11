# HarvestLot 판로 결과 카드

서버 Simulation의 `harvest-route-outcomes` 읽기 projection을 기존 정착지 HarvestLot 카드에 연결했다.

- 네 판로는 조합 출하, 온라인 직접 판매, 비축 보관, 외부 교역 준비의 고정 순서를 유지한다.
- 선택·진행·완료·수출 성공·수출 손실을 한국어 Presentation model로 표시한다.
- Unity는 수량이나 재정 결과를 다시 계산하지 않고 서버 응답을 형식화한다.
- 결과 조회 실패는 이미 확정된 Decision, Task, Effect와 정착지 snapshot을 되돌리지 않는다.

## 검증

- 집중 EditMode: 10/10 통과
- 전체 EditMode: 202/203 통과. 기존 연구 Scene 개수 기대 27개와 현재 28개의 불일치 1건
- Play Mode 비축 완료 경로 실행
- Unity Console 오류: 0건

![HarvestLot 판로 결과 카드](harvest-route-outcome-card.png)
