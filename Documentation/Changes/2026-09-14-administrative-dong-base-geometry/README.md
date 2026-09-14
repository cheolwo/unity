# 동북서울 30개 행정동 기초 디오라마 검토

2026-09-14 `SimulationWorldShell`의 실제 Play Mode Game View에서 광진구 4개, 동대문구 10개, 중랑구 16개 행정동을 한 동씩 순차 조립해 전체 조망 30장을 남겼다. 면목제3·8동은 건물 선택 상태의 근접 화면도 함께 남겼다.

![30개 행정동 전체 비교](00-administrative-dong-overview-contact-sheet.png)

## 대표 화면

| 화면 | 파일 | 의미 |
| --- | --- | --- |
| 면목제3·8동 전체 조망 | [20-1126057500-overview.png](modules/20-1126057500-overview.png) | 역사 행정동 경계 후보, 건물 외곽, 도로 중심선의 기초 배치 |
| 면목제3·8동 건물 근접 | [31-1126057500-myeonmok-3-8-detail.png](31-1126057500-myeonmok-3-8-detail.png) | Collider 없이 선택한 검토 건물의 외곽과 미결속 정보를 표시 |
| 전체 실행 기록 | [capture-manifest.json](capture-manifest.json) | 30개 이미지·투영·묶음 hash, 개수, Scene 불변성과 결손 코드 |

전체 조망 원본 30장은 [`modules/`](modules/)에 번호와 행정동 코드 순서로 보존한다.

## 검증된 범위

- 행정동 30개, 건물 61,897개, 도로 선분 11,769개, tile 297개를 한 번에 한 행정동만 활성화해 조립했다.
- 각 모듈은 Renderer 4개, MeshFilter 4개, Collider 0개이며 건물별 GameObject를 만들지 않았다.
- 실제 Play Mode와 실제 Game View 캡처를 사용했다.
- `SimulationWorldShell`의 실행 전후 SHA-256은 `A303CE0FBFFB7B7D88C18AC64B6F9208A9FA18263CBE64671FD43DCBF963F1DC`로 같고 Scene은 저장·dirty 처리되지 않았다.
- 검토 묶음 세대 SHA-256은 `C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC`이다.
- 30개 전체 비교 이미지 SHA-256은 `BCCF906636C422A2FAE0F739792C74D6BB43865A4D2E4AFB1BBED505767C05A3`이다.

## 아직 증명하지 않는 것

현재 화면은 `PrivateReviewOnly`인 역사 경계 기반 기초 형상이다. 현행 행정동 경계, 확정 도로명주소, 실제 필지 도형·출입구, 실폭 도로·골목·보도·횡단보도, 차로 방향·신호, 공개 사업체 결속, 생활인구, NPC·차량, 운영 생명주기는 아직 같은 판본으로 연결되지 않았다.

연락판에서 일부 동이 넓은 빈 바닥이나 분리된 건물 군집으로 보이는 것은 시각 마감 문제가 아니라 역사 경계와 현재 건물·도로 자료가 같은 기준일로 결속되지 않았다는 결손을 드러낸다. 따라서 이 화면은 사가정역 수준의 생활상 완결, 통행 가능, 게임플레이 준비, 공개 배포의 증거가 아니다.

실행 중 Unity Editor의 기존 SearchDatabase 초기화 경로에서 `ArgumentOutOfRangeException` 1건이 기록되었고 종료 시 `JobTempAlloc` 경고 2건이 있었다. 캡처 코드·행정동 조립 시험은 통과했지만 이 기존 Editor 경고를 전체 Console 무오류로 숨기지 않는다.
