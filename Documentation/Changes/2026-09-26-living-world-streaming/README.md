# 음식 생활상 표본 확대별 표현 수명 r46

2026-09-26, Unity 6000.5.6f1, canonical `SimulationWorldShell`의 실제 Play Mode·1280×720 Game View 기록이다. **구현 검토용 표본 화면이며 확정 UI 시안이나 실제 운영 화면이 아니다.** 음식 정상·회복 두 업무와 타 OS 보존 확인용 화물 한 업무를 명시적으로 주입했다. 운영 HTTP 조회나 실제 주문 완료의 증거가 아니다.

## 최종 확인

- [자동 캡처 기록](gameview-r46c/capture-manifest.json): `profilePassed=true`, PNG 6개 SHA-256 독립 대조 불일치 0개.
- 실제 Play 검증 구간: `2026-09-26T07:21:33.0747326Z` ~ `07:21:47.7565502Z`, 약 14.68초. 실제 5초 상세 해제 유예를 포함한다. 5분/10분 관찰·성능·메모리 안정성 검증은 아니다.
- 원본 manifest SHA-256: `D6B4E5F36CE26DA9F7E0DDAB72545FC30CB41258F2A665ADADFE8CDFAFAE4581`.
- manifest의 `humanVisualReviewStatus=Pending`은 자동 실행 당시 값이다. 원본을 덮어쓰지 않고, 이후 주 에이전트가 PNG 6개를 직접 열어 확인한 결과를 이 문서에 남긴다. 실제 사용자 승인을 뜻하지 않는다.
- 최종 시각 검토: 지구본이 앞을 가리지 않고 디오라마가 나타난다. 거리 확대·동 전체 보기·더 먼 구 표본 보기가 구분되고, 두 패널이 서로 겹치지 않는다. 최신 상태 복귀·만료 후 음식 행 제거도 읽힌다. 패널이 지도 상단을 많이 차지하는 검증용 구성으로, 애니메이션 중심 최종 UI 품질을 승인한 것은 아니다.
- 구·동 요약 모두 **사가정 한 지역 표본**이다. 새로운 행정동/중랑구 전체 지도 또는 전역 집계를 구현한 것이 아니다.

| 실제 확인점 | 음식 사본 | 음식 상세 표식 | 검증 내용 |
| --- | --- | --- | --- |
| 거리 최초 | 2 | 활성 2 | revision 8·7 |
| 동·구 표본 요약 | 2 | 숨김 2 | 사본은 유지 |
| 실제 5초 유예 후 | 2 | 0 | 상세 객체 해제 |
| 거리 재진입 | 2 | 활성 2 | 최신 revision 9·10 |
| TTL 만료 | 0 | 0 | 자동 삭제 |
| 명시적 Clear | 0 | 0 | 다른 OS 유지 |

모든 확인점에서 화물 표식은 동일 인스턴스를 유지했다. Scene을 저장하지 않았으며 실행 전후 SHA-256은 `c31167703c4a7683da374e237cba3c9584a5f1de4b5fb5746939a3632635ac65`로 같다. 전용 검증 Editor는 자체 종료했다.

## 화면

- [거리 상세](gameview-r46c/01-street.png)
- [동 표본 요약](gameview-r46c/02-neighborhood.png)
- [구 표본 요약](gameview-r46c/03-district.png)
- [최신 상태로 거리 복귀](gameview-r46c/04-street-latest-revision.png)
- [음식 사본 만료](gameview-r46c/05-expired.png)
- [명시적 해제·화물 보존](gameview-r46c/06-cleared.png)

## Console·검증 경계

Console 종료 집계는 오류 9건이고, 기록 구간의 Error 2·Exception 6은 총 8건이다. 기존 `SimulationReplayHashMismatch`, `TurnClosingServerRequestFailed`, `WorldTileStreamRequestFailed`, Farm·물류·입고 서버 세션 누락 등이 남아 있다. **Console 오류 0을 충족하지 않는다.** 다른 Controller는 임의로 끄지 않았으며 새 서버도 시작하지 않았다.

이 도구는 음식 관찰 Controller 하나만 표본 주입을 위해 중지한다. 제품의 확대/축소 경로는 Controller를 중지하지 않는다. 실제 클릭 입력·인증 HTTP·권한 철회·Windows 빌드·장시간 운용·실제 운영 효과는 검증하지 않았다. 객체/Material의 좁은 해제 회귀는 EditMode에서 검증했고, 전역 Material 수만으로 장시간 안정성을 주장하지 않는다.

공통 패키지 전체 849/849, 집중 회귀 99/99, Unity EditMode 37/37(새 표현 14개 포함), Unity solution build 경고/오류 0이다. 전역 Fast는 별도 E 책임 생성 지도의 불일치로 차단되었으며 전체 Task 통과를 주장하지 않는다.

## 실패 기록 보존

- `artifacts/local/validation/living-world-streaming/gameview-r46/`: 내부 검사는 통과했지만 실제 PNG가 지구본이었다. 화면 성공 증거에서 제외했다. 기존 공식 디오라마 전환 경로와 카메라 검사를 추가한 뒤 재촬영했다.
- `artifacts/local/validation/living-world-streaming/gameview-r46b/`: 다른 파일 수정에 의한 어셈블리 재로드로 중단, PNG 0개. 중간 실패 자료는 삭제하지 않고 ignored 검증 경로로 이동했다.

새 디오라마 규칙 후보 없음. 새 Scene·Scene 저장·DB 변경·commit·push·E 승격 없음.
