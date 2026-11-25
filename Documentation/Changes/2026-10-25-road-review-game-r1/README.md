# 도로 관찰 게임 첫 시안 r1

사용자가 직접 촬영한 장면을 게임처럼 비교하면서 도로 정보를 보완하도록 요청했다. 실제 원본 0103·0093·0101에서 기존 검토 후보와 사진 각 5장을 읽는 로컬 Unity 모듈을 추가했다. 런타임 모듈은 `Assets/Ssalddel/RoadReviewGame/`에 있으며 기존 운영·Simulation 어셈블리를 참조하지 않는다.

사진 앞뒤 이동, 간판 단서, 실제 좌표의 도로 도식, 확인한 선분 선택/해제, 보류, 다음 관측, 명시적 등록 확인을 구현했다. 다섯 사진을 현재 서버에서 모두 열어야 등록 검토가 활성화된다. 선택은 초안이고 등록은 기존 후보 승인 API를 사용한다. 통신 결과가 불확실한 경우 같은 요청 식별자와 본문으로 재시도하며 게임 재시작 뒤 자동 전송하지 않는다. 게임의 열람·보류 진행은 별도 JSON이며 승인 DB가 아니다.

`RoadReviewGameBuilder.Build`는 공식 `SimulationWorldShell`의 빌드 메모리 사본만 조립한다. 기존 Scene 파일을 저장하거나 새 공식 Scene을 만들지 않는다. 전용 컨트롤러는 런타임 Scene 이름만 바꿔 기존 업무 부트스트랩의 실행 조건과 분리한다. Editor 연습 메뉴도 비저장 사본을 사용하며 플레이 종료 뒤 진입 전 저장된 Scene 구성을 복원하도록 작성했다. 이 동작의 실제 Editor 왕복 검증은 남아 있다.

검증:

- 설치된 Unity Core/SharedInternals·Newtonsoft DLL을 참조하여 계약 코드를 .NET으로 컴파일하고 합성 계약 검사 29개 통과. 전체 UGUI 런타임의 Unity 타입 검사는 아니다.
- Roslyn C# 문법 검사: 신규 C# 4개, 오류 0.
- 기존 `test_segment_candidate_review` 시험 21개 통과. 격리된 합성 저장소를 사용한다.
- 실제 기존 서버에서 미션 3개 GET·증거 사진 15개 HTTP/이미지 디코드 성공. 전후 승인 상태/버전은 모두 `candidate / 0`이며 실제 승인 POST는 보내지 않았다.
- Unity EditMode 계약 시험 20개를 추가했지만 실행하지 못했다.
- Scene·ProjectSettings·EditorBuildSettings 해시가 시작 전과 동일하다. 기존 사용자 마커·검토 DB·원본·공유본은 변경하지 않았다.

Unity CLI batch와 GUI Editor 모두 `No valid Unity Editor license found`로 멈췄다. CLI의 Editor 종료 코드는 198이다. 연결된 Pipeline Editor도 없어 Game View·실제 입력·Windows Player 빌드는 확인하지 못했다. 사용자는 현재 라이선스 활성화를 할 수 없다고 답했으므로 이 범위를 완료로 표시하지 않는다. 렌더 이미지나 다른 앱 화면을 Unity 실행 증거로 대신하지 않았다.

미션과 사용법: `C:\Users\user\Documents\ChatGPT\배달\edit-kit\private\road-review-game\20261025-r1\사용법.md`. 활성화 뒤 로컬 실행 스크립트나 Editor 메뉴로 재개한다. MYBOX·SNS·운영 DB·기존 Unity 업무 상태에는 반영하지 않았다. commit/push는 하지 않았다.
