# 모바일 게임 세션 연결

`MobileGameSession.Attach(host, sampleJson, roadJson, storageDirectory)`로 한 번 연결하고 반환한 세션을 화면의 `AttachSession`에 전달한다. 파일 포트는 `SetFileExchange(IMobileGameFileExchange)`로 별도 주입한다. 생성 시 자동 새 게임을 시작하지 않으며 정상 저장이 있으면 일시정지 상태로 복원한다.

## 화면 명령

- `start`·`campaign-start`·`new-game`: 직접 픽업 확인을 사용하는 새 가상 회차. 기본 배속은 실제 1초당 가상 12초다.
- `set-speed`의 `speed=0/1/2/4`, `pause`, `resume`: 이 세션만 가상 시계를 진행한다. 화면에서 `tick`을 보내지 않는다.
- `candidates`·`assign`·`pickup-confirm`·`campaign-policy`: 기존 Core 입력을 사용하며 현재 세션이 가상 시각을 넣는다.
- `export`: 출처가 붙은 가상 운영 구성 파일을 저장하고, 연결된 Android 문서 저장 창에서 선택한 위치에 저장한다. 취소하면 기존 기기 사본과 회차를 유지한다.
- `import`: `importJson` 또는 `referenceJson`의 지원 자료를 검사한다. 문자열이 없으면 플랫폼 파일 선택을 사용한다.
- `ai-trial`: 선택한 `orderId`의 현재 적격 후보와 규칙 추천으로 가상 배차한다. 모델 추론이나 가중치 학습이 아니다.

`SnapshotJson`과 `Changed`는 Core 전체 응답에 `clock={speed,paused,resumeRequired}`와 필요한 한국어 `notice`를 붙인다.

## 저장과 복귀

배차·픽업·방침 확정, 일시정지·화면 이탈 및 실행 중 약 5초마다 Core의 확정 세이브를 저장한다. `.tmp` 작성·동기화 뒤 정상 파일과 이전 정상 파일을 교체한다. 복원은 원본·규칙 판본과 명령 재생을 검사하는 Core에 위임하며 최근 파일이 손상되면 이전 정상 파일을 시도한다. 손상 파일은 자동 삭제하지 않는다.

화면 이탈 시 멈추고 복귀 후에는 **이어하기**를 눌러야 한다. 백그라운드 경과 시간을 가상 시계에 더하지 않는다. 현재 프레임 지연도 0.25초로 제한한다.

## 자료·경험의 범위

반입 자료는 `game-business-preparation.v1` 또는 `reality-game-context.v1`의 1MB 이하 JSON이며 고객·계정·상세주소 필드를 거부한다. 출처·권리 확인을 대신하는 검사는 아니다. 반입은 비공개 참고 사본 저장이며 현재 게임 조건·현실 원장·모델을 자동 변경하지 않는다.

경험은 `game-experiences.v1.jsonl`에 Core와 같은 입력 지문·회차 ID·게임/정책 판본·후보·선택·성패를 보존한다. 플레이어 선택·규칙 시험·회차 확정은 `actorKind`로 구분한다. 수령·정산 완료 시 `sessionId:round-outcome` 사건에 숫자/불리언 결산과 주문별 기사 ID·전달 시간·제시간 지정 여부를 한 번 남긴다. 복원·재표시 후에도 같은 사건을 중복 추가하지 않으며 전체 상태·배송지·좌표는 경험 로그에 복제하지 않는다. 정책 지문은 판본 설명의 SHA이며 소스 코드 전체의 해시라고 주장하지 않는다.

사람 승인·학습 적격·가중치 학습·운영 실행은 모두 부여하지 않는다. 모델을 호출하지 않으며 모델 ID·digest는 미사용으로 남긴다. 실제 모델이 없는 첫 규칙 시험을 AI 학습 성공으로 표시하지 않는다.

플랫폼 파일 선택·공유, 실제 Android 실행과 복귀 검증, Release 빌드는 상위 조립 담당이 수행한다. 이 폴더는 Bootstrap을 자동 추가하거나 다른 실행판을 시작하지 않는다.
