# CULTURE-CARD-0 서울 생활문화 질문

`SimulationWorldShell` 턴 마감 패널에 첫 문화카드를 연결했다. 이 카드는 특정 행사나 지역 대표성을 주장하지 않고, 주민의 현재 경험과 공식 지역문화 원천을 함께 확인하도록 돕는 검수 Fixture다.

## 권위와 근거

- Card stable ID: `culture:kr-seoul.living-culture-question.2026`
- Region: `kr-seoul`
- 유효기간: 2026-01-01 ~ 2026-12-31 Simulation game date
- Calendar revision: `simulation-culture-calendar:kr-seoul:2026.r1`
- Effect rule revision: `culture-local-context-awareness:r1`
- 공식 원천: 지역문화진흥원 관계기관 정보, 2026-07-26 확인
- 다음 턴 효과: `LocalContextAwareness`, `CommunityInsight +1`

지역·기간·calendar revision·effect rule revision·HTTPS source·근거 확인 시각 중 하나라도 빠지면 서버와 Unity가 카드를 거부한다.

## Game View

- [문화카드 Preview](culture-card-seoul-preview.png): 현재 Tick과 Revision을 유지하면서 문화카드의 지역·달력 revision·근거 확인일을 표시한다.
- [Confirm 후 다음 날](culture-card-seoul-next-day.png): Day 14, Tick 13, Revision 13으로 전환하고 `LocalContextAwareness`를 활성화한다.

## 검증

- Unity 턴 마감 집중 EditMode: 4/4 통과
- 전체 Unity EditMode: 213/214 통과. 실패 1건은 기존 연구 Scene 기대 개수 27과 현재 28의 불일치다.
- Play Mode Preview→Confirm 골든 패스 통과
- 골든 패스 구간 새 Console 오류 0건
- 최초 Preview 진입 때 Unity 오디오 출력 장치 초기화 `FMOD 60` 환경 오류 1건이 있었으며 카드 코드 예외는 아니다.
