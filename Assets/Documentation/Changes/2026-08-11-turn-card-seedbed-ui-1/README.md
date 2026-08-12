# 턴 카드 모판 화면

## 결과

실제 게임 덱과 분리된 `턴카드모판` Scene을 추가했다. 철학·학당 모판과 지역문화 모판을 직접 전환하고 각 후보의 C0~C6 Gate, Fixture/게시 구분, source revision, effect rule revision, 확인된 범위, 알 수 없는 범위와 승격 차단 사유를 한국어로 확인할 수 있다.

이 화면은 연구 projection이다. `턴마감Presenter`, Simulation session, Preview·Confirm과 턴 진행 기능을 포함하지 않으며 모판을 전환하거나 후보를 선택해도 연구 revision 0은 변하지 않는다.

## 현재 후보

- 바보 `BeginnerMind`: 철학·학당 모판, C3·C4 Fixture 검증, C2·C5 차단
- 전차 `IntegratedProgress`: 철학·학당 모판, C3·C4 Fixture 검증, C2·C5 차단
- 서울 생활문화 질문: 지역문화 모판, C1 통과, C3·C4 Fixture 검증, 행사형 C2·C5 차단

실제 승인 publication은 0건이며 빈 게시 덱을 Fixture로 보완하지 않는다.

## Game View

![철학·학당 모판](philosophy-academy-game-view.png)

![지역문화 모판](regional-culture-game-view.png)

두 화면 모두 실제 Unity Play Mode Game View에서 확인했다. 새 Input System용 UI 입력 모듈을 사용하며 최종 전환 구간 Console 오류는 0건이다.

## 검증

- Scene Builder와 배선 검증 통과
- 집중 EditMode 3/3 통과
- 전체 EditMode 222/223 통과. 실패 1건은 기존 연구 Scene 기대 개수 27과 현재 28의 불일치다.
- 서버 호출·session 변경·실제 publication·commit·push 없음
