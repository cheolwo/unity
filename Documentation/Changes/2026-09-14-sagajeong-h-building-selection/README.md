# 사가정 건물 H 계층 선택 검증

## 결과

- 동결 지도 `sagajeong-reference.r3` 의 참고 건물 602개를 개별 GameObject나 Collider 없이 CPU 외곽 판정으로 선택한다.
- 건물 두 곳만 H1→H2→H3→부분 AreaSet 후보를 읽기 전용으로 표시하고, 나머지는 `WaitingForGraphMapBinding`을 표시한다.
- 높이 보완과 `LocalPrivateReview` 공간 보완이 r3 건물을 대체한 경우, 화면에 그린 높이·외곽과 동일한 표현 기둥으로 클릭을 판정한다.
- 기존 건물·도로 Mesh, 카메라, 색, Scene은 수정하지 않았다. 선택 표시는 IMGUI 표식과 우측 카드만 사용한다.

## Game View

### 선택 전 전체 조망

![선택 전 전체 조망](01-overview-unselected.png)

### H 후보 건물 선택

![H 후보 건물 선택](02-overview-selected-h.png)

### 선택 건물 근접 조망

![선택 건물 근접 조망](03-closeup-selected-h.png)

## 검증 범위

- 격리 Unity EditMode: 선택 8건 + 기존 사가정 모형·운영·공간 회귀 65건, 총 73/73 통과
- 실제 canonical `SimulationWorldShell` Play Mode에서 그래픽 프로세스로 1399×628 Game View 3장 캡처
- 선택 전후 Renderer 39, Material 5, MeshFilter 39 유지; Collider 0
- Scene SHA-256 실행 전후 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55` 일치, Scene dirty false
- 화면 ray 선택은 검증 Driver의 직접 호출로 확인했다. 사용자 MouseUp 입력 주입은 이 증거에 포함하지 않는다.

## 증거 상한과 기존 Scene 경고

- 이 캡처는 `LocalPrivateReview`, `publicReleaseEvidence=false`인 로컬 검토 증거다. Steam 게시·공개 배포·사진 권리 승인을 뜻하지 않는다.
- 선택은 Unity 로컬 표현 상태만 바꾼다. Graph Map·H 대장·Simulation·WorldRevision·WI·ActionRecord는 변경하지 않으며, Runtime 원장 사본 비교는 이번 캡처의 검증 범위가 아니다.
- 기존 Scene에서 누락 Prefab GUID 56개, Unknown script 10건, `SimulationReplayHashMismatch`, `TurnClosingServerRequestFailed`/localhost 연결 실패, JobTempAlloc 경고가 재현됐다. H 선택은 정상 캡처됐지만 이 실행을 전체 Scene 건전성이나 live 서버 연결 증거로 확대하지 않는다.

## 파일 SHA-256

- `01-overview-unselected.png`: `526A49466B5C015DBD90C7E790EF24C412B58284DE6337506D9C08FE36D1D25D`
- `02-overview-selected-h.png`: `6148390264EF9DCD30D02A244ED61F51A90DAF0FD513B7FE6E019C0CF6205770`
- `03-closeup-selected-h.png`: `689EFC00F2CC271D524762560203178A887A9DAB69F4DCFFA865BF08553D625F`
