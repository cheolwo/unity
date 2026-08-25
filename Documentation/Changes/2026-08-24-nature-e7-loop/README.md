# 2026-08-24 Nature E7 검증 폐루프

canonical `SimulationWorldShell` 하나에서 Nature 생활 거점의 실제 입력 경로를
검증하기 위한 좁은 Solo 로컬 조각이다.

## 구현 범위

- 기본 20분 주기는 유지하고 `SSALDDEL_NATURE_E7_VALIDATION=1`일 때만
  황혼 90초 전 상태로 시작한다.
- 기본 슬롯과 E7 검증 슬롯을 분리하고, 같은 공통 Session 전체를 명시적 HUD
  저장으로 봉인한 뒤 재진입 때 복원한다.
- 슬롯 없음만 새 Session으로 처리하며 손상 슬롯은 새 게임으로 숨기지 않는다.
- 도끼 확보, 벌목 취소와 자원 반환, 세 나무 벌목, 오두막 도면·건설,
  입장·`E` 퇴장, HUD 저장과 장면 재진입 복원을 기존 H2/H1 결속과 공통
  Simulation Runtime 위에서 처리한다.
- 조준선은 앞쪽의 다른 Collider 뒤에 Nature 표식이 있어도 거리 안의 첫 표식을
  선택하며, 입력 충돌·조준 실패·저장 실패를 HUD 상태로 드러낸다.

## 검증 상태

- Unity EditMode 집중 시험 `16/16`이 통과했다.
- Unity PlayMode 실제 입력 폐루프 `1/1`이 53.3초 경로로 통과했다. 시험 전용
  Core 완료 메서드 없이 Input System으로 도끼 클릭, `C` 취소, 지속 누르기,
  `B` 배치, 입장·`E` 퇴장, HUD 저장 클릭을 수행했다.
- Synty 도끼 단일 WI PlayMode `1/1`, 감각 표현 PlayMode `1/1`, Simulation
  Nature 집중 시험 `36/36`이 통과했고 PlayMode 프로젝트 빌드는 오류 0개다.
- 저장 Scene은 변경하지 않았다. 배치 모드 로그에는 기존 FarmPlot·HarvestLot
  직렬화 자동 보정 메시지와 Unity 라이선스 갱신 오류가 남아 있다.
- 사람의 Game View 조작·음향 청취·최종 PNG와 외부 프로세스 RemoteHost는
  아직 이번 자동 증거가 아니다.
- 실제 E5 Network, Hosted 서버, Farm 연결 또는 전체 World의 E7 완료를 주장하지
  않는다.
