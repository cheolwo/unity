# EVENING-1 저녁 학당

낮의 농장·물류 진행과 분리된 밤 21시 학습 화면을 추가한다. 첫 콘텐츠는 홍익학당 타로 설명의 `0. 바보`이며, 핵심을 무모함이 아니라 `무분별한 마음 · 모를 뿐`으로 제시한다.

## 구현 경계

- 화면의 Night/Dawn 조명은 `월드시간대Presenter`가 표현한다.
- 실제 변경은 `저녁학당SimulationEngine`의 Preview → Confirm → Tick만 수행한다.
- Tick 뒤 다음 날 `알아차림 +1`, `BeginnerMind`가 활성화된다.
- 정착지의 재정·노동·창고·식량 상태와 플레이어 내면 상태는 섞지 않는다.
- 출처는 영상 ID `qo1tNkwSBVs`, 시작 `5339s`, 지식 노트 revision `4`로 화면에 표시한다.
- 오전 행동 원장으로 LLM 추천 요청을 만들고, 응답은 허용된 콘텐츠 ID와 실제 행동 인용만 수락한다.
- LLM은 추천 이유만 만들며 스탯·규칙 효과는 콘텐츠 catalog가 소유한다.
- fallback은 `UnknownSkipped → 바보`, `CargoLoaded/JourneyStarted/CompetingForces → 전차`다.
- 전차는 강의 근거대로 긍정적인 통합·정진으로 해석해 `의지 +1 / IntegratedProgress`를 적용한다.

## 시각 검증

이번 단계는 사용자 요청에 따라 Game View 캡처를 생략했다. Scene 생성과 집중 Unity EditMode 3/3은 통과했다.
