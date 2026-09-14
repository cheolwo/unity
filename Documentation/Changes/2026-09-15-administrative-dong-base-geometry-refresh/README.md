# 동북서울 30개 행정동 기초 디오라마 재검증

2026-09-15 전용 격리 Unity 작업공간에서 동북서울 30개 행정동의 기초 형상을 다시 조립하고 실제 Play Mode Game View를 캡처했다. 본 작업공간에서 열려 있던 Unity Editor와 Scene은 점유하거나 저장하지 않았다.

![30개 행정동 전체 비교](00-administrative-dong-overview-contact-sheet.png)

## 보존 자료

| 자료 | 파일 | 의미 |
| --- | --- | --- |
| 30개 전체 비교 | [00-administrative-dong-overview-contact-sheet.png](00-administrative-dong-overview-contact-sheet.png) | 행정동별 기초 형상을 5열 × 6행으로 비교 |
| 면목제3·8동 전체 조망 | [20-1126057500-overview.png](modules/20-1126057500-overview.png) | 역사 행정동 경계 후보·건물 외곽·도로 중심선 |
| 면목제3·8동 근접 | [31-1126057500-myeonmok-3-8-detail.png](31-1126057500-myeonmok-3-8-detail.png) | 한 검토 건물을 선택한 근접 화면과 미결속 진단 |
| 전체 실행 기록 | [capture-manifest.json](capture-manifest.json) | 입력·투영·이미지 hash, Scene 불변성, 권위 제한 |
| EditMode 결과 | [editmode-results.xml](editmode-results.xml) | `행정동디오라마검토Tests` 9/9 통과 원본 |

전체 조망 원본 30장은 [`modules/`](modules/)에 행정동 코드 순서로 보존한다.

## 재검증 결과

- 행정동 30개, 건물 61,897개, 도로 중심선 선분 11,769개, tile 297개를 한 번에 한 행정동씩 조립했다.
- 전체 조망 30장과 면목제3·8동 근접 1장을 실제 Play Mode Game View로 다시 캡처했다. 연락판을 포함한 PNG는 총 32개다.
- 30개 전체 조망 hash는 모두 서로 다르고, 30개 전체 조망과 근접 화면은 2026-09-14 기준선의 대응 이미지와 모두 동일하다.
- `actualPlayMode=true`, `actualGameViewCapture=true`, `sceneSaved=false`, `sceneDirtyAfterPlay=false`, `sceneHashUnchanged=true`를 확인했다.
- 캡처 입력 묶음 SHA-256은 `C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC`, 원본 index SHA-256은 `C8D4999494767E9588E998100893A2D4227A8E116DEBD8F8B81611BA94D3E123`이다.
- 연락판 SHA-256은 `92F7CC20942E9C96A73C2900C3E3A252BC56470B3C0A5792FFDCDFF34402D0A7`, 실행 기록 SHA-256은 `5FADD014035E79FF8D80FE5325EB51E748D0F72AD91494D257EAE3B3C2C78725`, EditMode XML SHA-256은 `FF84DF33A17340E4DA54CE675A1ED650A95C9B3DA4A9F1BAC8767458D2E88325`이다.

## 격리와 Scene 불변성

- 캡처는 `C:\Users\user\ssalddel-admin30-validation`에서 수행했다. 실행 시 본 작업공간과 격리 작업공간의 캡처 전용 소스 10개는 바이트 단위로 같았다.
- 두 작업공간의 `SimulationWorldShell.unity`는 개행을 정규화하면 내용이 같았다. 본 작업공간 raw SHA-256은 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`, 격리 작업공간 raw SHA-256은 `A303CE0FBFFB7B7D88C18AC64B6F9208A9FA18263CBE64671FD43DCBF963F1DC`로 개행 바이트가 달랐다.
- 실행 기록의 Scene 전후 hash는 격리 작업공간 raw 파일 기준 `A303CE0FBFFB7B7D88C18AC64B6F9208A9FA18263CBE64671FD43DCBF963F1DC`로 동일했다. 어느 Scene도 수정하거나 저장하지 않았다.

## 이 화면이 증명하지 않는 것

이 재검증은 `PrivateReviewOnly` 역사 경계 기반 기초 형상만 대상으로 한다. G2 주소·필지 후보, G3 횡단보도·교차로 후보, G4 사업장 후보를 화면에 합성하지 않았고 NPC·차량·신호·주문·배차·운영 생명주기도 실행하지 않았다.

따라서 현행 행정동 경계, 확정 도로명주소, 실제 필지 도형·출입구, 실폭 도로·골목·보도, 차로 방향·신호 현시, 공개 사업체 결속, 이동 가능성, 생활상 완결, 게임플레이 준비 또는 공개 배포의 증거가 아니다. `currentPointerUsed=false`, `distributionApproved=false`, `traversalReady=false`, `gameplayReady=false`, `lifeDioramaParityVerified=false`를 유지한다.

실행 중 기존 Unity SearchDatabase 초기화 경로의 `ArgumentOutOfRangeException` 1건과 종료 시 `JobTempAlloc` 경고 2건이 다시 기록됐다. 캡처 코드의 컴파일 오류는 0건이고 행정동 시험과 캡처는 통과했지만 전체 Console 무오류로 확대하지 않는다.
