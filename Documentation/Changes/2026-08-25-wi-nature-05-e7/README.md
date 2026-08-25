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
- Console: Play 시작 뒤 오류 0건. 저장 장면의 기존 `The referenced script (Unknown) ... is missing` 경고 10건은 남아 있으며 이번 WI 완료 근거로 숨기지 않는다.
- Hosted 동등성: 실제 Solo 경계 `LocalSimulationRuntime`과 Hosted HTTP Controller 경계에 동일 Fixture·명령열을 적용해 `simulation-save.v23` revision과 replay hash가 같은지 검증했다.
- 판정: E4·E5·E6은 통과, E7은 `Partial`이다. 외부 프로세스 RemoteHost 실행과 저장 장면 Missing Script 경고 정리는 후속 관문으로 남긴다.

같은 자동 검증 묶음에서 Nature 생활 거점 전체 실제 입력 폐루프 `1/1`,
EditMode `16/16`, 감각 표현 PlayMode `1/1`, Simulation Nature 집중 시험
`36/36`도 통과했다. 이는 후속 WI의 회귀 안전망이며 `WI-NATURE-05` 이외의
성숙도를 자동 승격하지 않는다.

## 증거 위치

- Unity PlayMode XML: `artifacts/local/validation/nature-shelter-actual-input/axe-e7-playmode-results.xml`
- Nature 폐루프 XML: `artifacts/local/validation/nature-shelter-actual-input/playmode-results-12.xml`
- Nature EditMode XML: `artifacts/local/validation/nature-shelter-actual-input/editmode-results-2.xml`
- 서버 집중 TRX: `artifacts/local/validation/wi-nature-05-server-targeted/wi-nature-05-server-targeted.trx`
- LocalProcess·Hosted HTTP 동등성 TRX: `artifacts/local/validation/wi-nature-05-host-parity/wi-nature-05-host-parity.trx`
