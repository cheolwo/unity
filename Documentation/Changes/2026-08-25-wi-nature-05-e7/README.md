# WI-NATURE-05 벌목 도끼 확보 E7 검증

## 범위

- 공식 진입 장면 `SimulationWorldShell`만 사용한다.
- 설치된 Synty Polygon Generic 도끼를 안전 빈터의 H1 선택 대상으로 표현한다.
- 실제 Input System 마우스 왼쪽 입력으로 도끼를 확보한다.
- 확보 전후 권위 revision, 도끼 표현 제거, 상태 안내, 로컬 저장과 장면 재진입 복원을 검증한다.

## 경계

- 이 증거는 `WI-NATURE-05` 하나만 대상으로 한다.
- 벌목·통나무 획득·오두막·황혼 대응이나 Nature 부모 폐루프를 완료로 승격하지 않는다.
- Synty 자산은 표현 근거이며 도끼 효율이나 세계 규칙의 권위 근거가 아니다.

## 검증 결과

- Play Mode: `Nature도끼확보E7PlayModeTests` 1/1 통과. 실제 Input System 클릭, 권위 revision 증가, 도끼 표현 제거, 로컬 저장과 같은 세션·revision 복원을 9.5초 경로로 확인했다.
- Game View: 검증 전용 환경의 공식 `SimulationWorldShell`을 수동 실행해 Nature E7 상태판과 Synty 공간 표현을 확인했다. 검증과 무관한 창고 진단·서버 오류 UI는 표시되지 않았다.
- Console: 신선한 Editor 프로세스에서 저장 Scene 전체를 검사했을 때 실제 Missing MonoBehaviour는 `0`개였다. 다만 같은 Scene을 두 번 여는 실제 입력 PlayMode에서는 두 번째 로드 때 Farm 객체 5개의 `references runtime script in scene file. Fixing!` 자동 보정과 `Unknown` script 경고 7개가 계속 기록된다. 테스트는 통과하지만 이 직렬화 경고와 Unity 라이선스 갱신 오류를 Console 무결성 완료로 숨기지 않는다.
- Hosted 동등성: 실제 Solo 경계 `LocalSimulationRuntime`과 별도 `dotnet` 프로세스의 RemoteHost에 취소·재수확·오두막 건설·입장·퇴장·저장·Replay verification 명령 15개를 적용했다. 양쪽 최종 revision은 `15`, 저장 판본은 `simulation-save.v23`, replay hash는 `83a5582724e76d95f8a3344bf86ee7ea56703edbe708aaaf1fa920a131ede0e0`으로 일치했다.
- 판정: E4·E5·E6은 통과, E7은 `Partial`이다. 외부 프로세스 RemoteHost 관문은 닫혔지만 전체 생활거점의 수동 Game View 조작·실제 청음과 PlayMode 재로드 직렬화 경고 분리는 후속 관문으로 남긴다.

같은 자동 검증 묶음에서 Nature 생활 거점 전체 실제 입력 폐루프 `1/1`,
EditMode `16/16`, 감각 표현 PlayMode `1/1`, Simulation Nature 집중 시험
`36/36`도 통과했다. 이는 후속 WI의 회귀 안전망이며 `WI-NATURE-05` 이외의
성숙도를 자동 승격하지 않는다.

## 증거 위치

- Unity PlayMode XML: `artifacts/local/validation/nature-shelter-actual-input/axe-e7-playmode-results.xml`
- Nature 폐루프 XML: `artifacts/local/validation/nature-shelter-actual-input/playmode-results-13.xml`
- Nature EditMode XML: `artifacts/local/validation/nature-shelter-actual-input/editmode-results-2.xml`
- 서버 집중 TRX: `artifacts/local/validation/wi-nature-05-server-targeted/wi-nature-05-server-targeted.trx`
- LocalProcess·Hosted HTTP 동등성 TRX: `artifacts/local/validation/wi-nature-05-host-parity/wi-nature-05-host-parity.trx`
- 외부 RemoteHost 상세 결과: Hongdal `docs/Changes/2026-08-25-nature-shelter-remote-host-parity.md`
