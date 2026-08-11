# HarvestLot 판로 결과 재접속 갱신

기존 Simulation session에 다시 들어오거나 후속 물류 작업이 진행된 뒤, 최신 session snapshot과 판로 결과 목록을 같은 revision으로 다시 맞춘다.

- session snapshot을 먼저 읽고 판로 결과 목록을 조회한다.
- 두 응답의 session, revision, WorldTick이 모두 일치할 때만 화면 상태를 한 번에 교체한다.
- 불일치나 조회 실패 시 기존 카드와 WorldShell snapshot을 보존한다.
- 메모리에 Preview가 없어도 한 개의 HarvestLot 판로 결과를 복원한다.
- 재접속 상태에서 기존 Preview를 추정해 WorldTick 명령을 만들지 않는다.

## 검증

- 집중 EditMode: 12/12 통과
- 전체 EditMode: 204/205 통과. 기존 연구 Scene 개수 기대 27개와 현재 28개의 불일치 1건
- Play Mode 재접속: 외부 교역 준비, 가상 국제 운송 중, 출고 예약 300kg, revision 40 복원
- Unity Console 오류: 0건

![재접속한 외부 교역 운송 중 카드](harvest-route-refresh-in-transit.png)
