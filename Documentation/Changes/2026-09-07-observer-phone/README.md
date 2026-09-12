# 동네 관찰·운영 휴대폰

`NPC 직업 생활 · 30분` 프로필에서 `SimulationWorldShell`을 Play하고 오른쪽 아래 `휴대폰`을 연다. `동네 → 재생`으로 시작하며 휴대폰을 닫아도 재생은 유지한다.

- 동네·주문·NPC·창고·정책 앱과 뒤로/홈/닫기, 주문→NPC/시설 관찰을 연결했다.
- 기존 Session 상태를 조회하고 정책·Tick·저장은 기존 Runtime으로 보낸다. 순수 Core/저장 작업을 worker로 분리하며 수동 입력은 자동 저장 뒤 한 건만 대기한다.
- 기존 도형 세계·역할색·공식 Scene·저장 형식·r1~r4는 보존한다. 하단의 기존 영역 전환 탭도 보존했다.
- Unity 컴파일 오류 0·EditMode 8/8. 실제 두 해상도 클릭·정책 초안 유지와 적용·스크롤/카메라 분리를 확인했다. 실제30분15.9초 관찰에서 음식/마트/보충 수령6/6/2와 근무 NPC7명 휴식·복귀, Tick694/Revision1354 저장 재진입을 확인했다. 저장 지연으로 내부 시각은11:34에 그쳤으며 1800 Tick 실시간 완주는 미달이다.
- 아래 PNG는 최종 소스의 실제 Play Mode에서 uGUI를 포함해 캡처한 원본이다. 전체 1800 Tick 완료·운영 서버 연결·E 승격의 증거가 아니다. Scene 저장·커밋·푸시 없음.
- 별도 복사 슬롯의 가속1800 Tick 완료와 실제 새 표본 취소/확인→Tick0 정지/저장 성공도 확인했다. 실시간 원본과 분리된 기능 시험이다. 마지막/최대 저장4749.7/21404.3ms, Escape 미검증과 별도 Editor/검증 정리 오류2건은 Hongdal 결과 문서에 기록했다.
- Editor는 Play 종료·Scene clean·원래 저장 경로/프로필·Free Aspect·창 크기로 반환했다. 임시 해상도는 제거했고 배율만 초기1.1x 대신1.0x로 남았다.

| 화면 | 원본 |
| --- | --- |
| 닫힘·아이콘 | [1280×720](final-1280-closed.png) |
| 휴대폰 홈 | [1280×720](final-1280-home.png) |
| 동네 앱 | [1920×1080](final-1920-neighborhood.png) |
| 정책 앱 | [1920×1080](final-1920-policy.png) |
| 30분 실제 관찰 후 저장 재진입 | [1920×1080](final-1920-restored.png) |

![휴대폰 홈](final-1280-home.png)

승인 기획·사용법·코드 관계·최종 검증의 단일 기준은 Hongdal 저장소의 `docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/observer-phone-result.r1.md`다.
