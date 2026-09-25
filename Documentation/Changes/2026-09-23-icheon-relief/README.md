# 이천 실제 높이 로컬 검토

2026-09-23. 실제 DSM을 사용한 3km 지형 표본. `Tools/Mirror/이천 실제 높이 지형 검토`에서 Hongdal artifacts의 `unity-preview.json`을 선택한다. SimulationWorldShell Play Mode 전용이다.

`overview.png`와 `near.png`는 Play 중 이천 전용 카메라 렌더다. 전체 화면 screenshot인 `game.png`에는 지구본이 찍혀 최종 통합 화면 증거가 아니다. 자동 지구본 확대 전환은 아직 연결하지 않았다.

DSM 높이차177.0873m, 배율1, 정점10201/삼각형20000. 법선·계약 오류 거부·삭제 검사를 eval로 수행했다. 클릭 시험/EditMode suite/빌드 없음. 기존 서버 세션·연결·Replay 오류8건이 있어 Console 무오류라고 보고하지 않는다.

Copernicus 공개 DSM과 Sentinel-2 2025-11-12 영상을 같은 30m 격자로 정렬한 비배포 검토 자료다. 원영상이 정점색으로 축소되어 흐리며 공장·실도로 검증이 아니다. 자세한 출처와 이용조건은 Hongdal의 `icheon-terrain-research.r8.md`, 구현 기록은 `icheon-local-preview.r10.md` 참조. 원본자료는 Unity Assets에 포함하지 않는다.
