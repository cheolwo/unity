# 방문 검토 지도 진입 r54

최종 검증은 `run-02`다. Unity `6000.5.6f1` 실제 Play Mode·Game View의 표현 시나리오는 통과했다. `Run` 시작부터 종료 요청까지는 Scene 로드·Play 전환을 포함해 약 18.88초다. 기존 `SimulationReplayHashMismatch` Console 오류 1건 때문에 전체 `profilePassed=false`이며 `scenarioChecksPassed=true`다. 오류 없는 전체 실행이나 운영 통합 완료를 주장하지 않는다.

`방문검토지도진입검증.Run`은 전용 graphics Editor에서 canonical `SimulationWorldShell`을 저장 없이 열고, 기존 root를 메모리에서만 비활성화하여 한 지도 표현을 격리한다. NPC·운영 Controller의 연속성이나 서버 통합 검증은 아니다. 통합 담당이 단독 Editor 점유를 지정한 뒤에만 실행한다.

명시적으로 준비할 자료는 동결 r7 면목제3·8동 완전 지도(12타일·건물 2700·도로 291, 기존 6종 비공개 overlay)와 사가정 250m focus다. 실제 기록 7건은 신뢰된 위치 결속 없이 재생할 수 없으며, 표시 0개인 거절 결과를 별도로 기록한다.

재생 검증은 역사 경계와 focus 내부에 있는 중립 두 점의 `A → B → A` 합성 순서다. 화면에 “검토용 합성 순서 · 실제 배달 내역 아님”을 표시한다. 실제 매장·위치·주행·픽업 완료의 근거로 사용하지 않는다.

실행 인자:

```text
-executeMethod Ssalddel.Unity.Editor.방문검토지도진입검증.Run
-visitReviewMapEntryCapture
-visitReviewOutput <새 검증 출력 폴더>
```

환경 변수 `SSALDDEL_R53_REPO_ROOT`, `SSALDDEL_R53_RECORD_PATH`는 기존 비공개 입력을 가리킨다. `-batchmode`, `-nographics`, `-quit`는 사용하지 않는다. 임시 로그·중간 산출물은 `artifacts/visit-review-map-entry-r54/` 아래에 둔다.

실제 검증에서는 원래 활성 root 5개를 일시 격리했다. 실제 7건은 준비 진단 7개와 `VisitPlaybackPreparationRequired`로 거절되어 marker 0개를 유지했다. 합성 방문 3개·중립 두 점은 focus·역사 경계 검사를 통과했고, 실제 시계로 이동한 뒤 일시정지 유지·방문 3 선택·재생 정리·창 소유 host 해제를 확인했다.

| 실제 Game View | 확인 내용 |
| --- | --- |
| [01-actual-record-rejected.png](01-actual-record-rejected.png) | 실제 7건 보류, 방문 표시 0개, 동결 지도와 focus만 표시 |
| [02-synthetic-paused.png](02-synthetic-paused.png) | 합성 1→2 이동 뒤 일시정지, 작은 중립 표식 1개 |
| [03-synthetic-seek.png](03-synthetic-seek.png) | 합성 방문 3을 선택해 첫 표본 위치로 귀환한 정지 상태 |
| [04-cleared.png](04-cleared.png) | 방문 표식과 focus 정리, 원래 전체 지도 시점 복원 |

표식은 확대 화면의 왼쪽 도로 주변에 있는 작은 마름모로, 실제 기사·매장 위치가 아니다. 이 화면은 기존 지도 위의 검토 진입과 순서 재생 수명을 확인하는 용도다. 위치 표시의 크기·가독성을 최종 시각 디자인으로 확정한 것은 아니다. 네 PNG 모두 1280×720이고 [capture-manifest.json](capture-manifest.json)의 SHA-256과 독립 대조했다. 화면에서 실제 방문 원문·주소·개인정보는 표시하지 않았다.

Console은 시작 오류 0개, 종료 오류 1개, 기록 구간 Error/Exception/Assert 합계 1개, Warning 9개다. 남은 고정 오류는 기존 `Nature생존AutoBootstrap`의 로컬 저장 재생 경로에서 발생한 `SimulationReplayHashMismatch`다. 첫 회차의 실행기 `GameViewSizeGroup.RemoveCustomSize` 인덱스 오류는 전체 index를 사용하고 실제 제거 수를 확인하도록 수정했으며 최종 회차에서 재발하지 않았다. 원문 Console·stack·실제 기록·경로·식별자를 manifest에 복사하지 않았다.

canonical Scene은 저장하지 않았으며 종료 후 다시 열어 clean을 확인했다. 전후 SHA-256은 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 동일하다. 임시 Game View 크기를 제거하고 원래 크기를 복원했으며 `ProjectSettings.asset` SHA-256도 기존 `1173FB0729CBD9720435656B7B3EB0DDAF11F43010D4D02E641897698ED7D18D`와 같다. Pipeline 연결이 없어 전용 CLI graphics Editor와 `ScreenCapture`로 검증했다. 수동 UI 입력·Windows 플레이어 빌드는 미검증이다.

외부 수집·API·DB·운영 효과, Scene 저장, E 승격, commit·push는 포함하지 않는다. r7 출처·이용 제한과 실제 위치 미확정은 유지한다. 새 디오라마 규칙 후보 없음.
