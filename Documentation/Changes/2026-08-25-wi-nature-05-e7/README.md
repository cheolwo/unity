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
- Game View: 고유 Unity Editor 창에서 공식 `SimulationWorldShell`을 열고 Console을 비운 뒤 Play Mode를 직접 시작했다. Nature Solo HUD와 Synty 공간, `1인칭`·`3인칭` 버튼의 실제 시점 전환, 1인칭 조준선·도끼 View, 대상이 없을 때의 상호작용 실패 안내, `현재 세계 저장` 버튼 입력을 확인했다. 저장 Scene은 다시 저장하지 않았다.
- 감각 표현: Play Mode 계층에서 Player `AudioListener`, Nature 영역 환경음·음악 선택 채널, 2D UI 채널과 배치 기준점별 3D 효과음 `AudioSource` 생성을 확인했다. 도끼 획득·타격·취소·나무 낙하는 절차형 fallback clip과 `PlayOneShot` 경로를 사용하며 감각 표현 PlayMode `1/1`이 cue·Animation·FX의 읽기 전용 투영을 검증한다. 승인 Ambient·BGM 자산은 아직 없어 두 선택 채널의 clip은 비어 있고, Editor 오디오 장치 초기화도 실패했으므로 실제로 소리를 들었다고 판정하지 않는다.
- Console: 신선한 Editor 프로세스에서 저장 Scene 전체를 검사했을 때 실제 Missing MonoBehaviour는 `0`개였다. 같은 Scene을 두 번 여는 실제 입력 PlayMode에서는 두 번째 로드 때 Farm 객체 5개의 `references runtime script in scene file. Fixing!` 자동 보정과 `Unknown` script 경고 7개가 계속 기록된다. 이번 직접 Play Mode는 오류 `0`에서 시작했지만 잠시 뒤 범위 밖 오늘 작업 계획 모듈의 `DailyWorkServerRequestFailed:0:ConnectionError`가 1건 발생했다. 테스트 통과를 이 직렬화 경고·서버 연결 오류·Unity 라이선스 갱신 오류의 해소로 확대하지 않는다.
- Hosted 동등성: 실제 Solo 경계 `LocalSimulationRuntime`과 별도 `dotnet` 프로세스의 RemoteHost에 취소·재수확·오두막 건설·입장·퇴장·저장·Replay verification 명령 15개를 적용했다. 양쪽 최종 revision은 `15`, 저장 판본은 `simulation-save.v23`, replay hash는 `83a5582724e76d95f8a3344bf86ee7ea56703edbe708aaaf1fa920a131ede0e0`으로 일치했다.
- 판정: E4·E5·E6은 통과, E7은 `Partial`이다. 외부 프로세스 RemoteHost와 자동 실제 입력 전체 폐루프 관문은 닫혔고 직접 Game View에서 시점·실패 피드백·저장 입력까지 관찰했다. 다만 사람이 도끼 획득부터 저장 복원까지 전 과정을 수동 완주한 증거, 실제 청음, 범위 밖 서버 연결 오류와 PlayMode 재로드 직렬화 경고의 분리는 후속 관문으로 남긴다.

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
