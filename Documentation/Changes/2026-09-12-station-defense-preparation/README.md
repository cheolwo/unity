# 사가정역 방어 준비 · 첫 옥상 배치

2026-09-12. 공식 SimulationWorldShell의 기존 지도/카메라를 재사용한다. 독립 준비 Core 사본을 읽어 옥상 A/B와 가상 인원 하나를 표시한다. 운영 표식은 표시만 숨기며 업무 데이터 갱신·만료는 계속된다.

![옥상 A 배치](roof-a.png)

[B 재배치·모드 복귀 후 전체 화면](whole-b.png)

실제 Play Mode·OnGUI 포함 화면. View API 직접 호출로 A 배치·B 재배치·운영 복귀·준비 재진입을 검증했다. 자동 마우스 전달은 성공하지 않아 실제 클릭은 미검증이다. 확대 캡슐은 가상 기호이며 실제 옥상 안전/출입·사격을 뜻하지 않는다. 지도 r3와 별도 배치 JSON의 해시 결속을 검사한다.

최종 컴파일 오류 0, 준비 EditMode 8/8. 같은 작업의 운영 표시 5/5·지도 7/7 통과. 기존 Scene 정책 분류 1건과 시작 시 MissingScript/Replay/서버 진단은 별도다. 전투·이동·경제·피해·영속 저장은 범위 밖이며 새 구현 commit/push는 하지 않았다.

상세 기준: main 저장소 `docs/AI/Planning/시스템/PLAN-GAMEPLAY-NEIGHBORHOOD-DEFENSE/`의 기획·연구 r2·구현 기록. 실행: Play → `Ssalddel/통합 월드/운영 관찰/사가정 전체 보기` → `역 방어 준비`.
