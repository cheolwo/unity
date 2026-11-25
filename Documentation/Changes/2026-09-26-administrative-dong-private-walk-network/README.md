# 30개 행정동 비공개 보행망 Game View 증거

이 폴더는 역사 행정동 경계에 결속한 비공개 보행망 후보를 Unity `6000.5.6f1`의 실제 Play Mode와 Game View에서 검토한 로컬 증거다. 공개 지도, 현재 통행 가능 경로, NavMesh, 배달 경로 또는 gameplay 증거가 아니다.

## 입력

- generation: `29764F2B861BF00D60951D23F6F03B9D59281D28C0DF447E2F0AAE35F2972915`
- index SHA-256: `63B8EEAC823E1CD5F8E4D636331419B4A7292148D1E09CB84AB77EC3FC57C977`
- 자료: 서울 열린데이터광장 `OA-21208`의 2020년 기준 도보 네트워크와 `OA-22160`의 2023-10-31 역사 행정동 경계
- 표현 수량: 노드 17,267개, 링크 조각 23,472개, 링크 경로점 54,574개, 노드와 경로점 합계 71,841개
- 기존 관찰 표현: 횡단보도 1,533개, 교차로 554개

원본 식별자는 hash stable ID로 바꿨지만 bundle은 좌표 원점과 공통 ENU millimeter 점·선을 포함한다. 따라서 이 폴더와 입력 generation은 정밀 위치를 복원할 수 있는 로컬 비공개 자료로 취급한다.

## 실제 실행 결과

- `modules/`: 30개 행정동 overview PNG
- `31-1126057500-myeonmok-3-8-detail.png`: 면목제3·8동 선택 건물 detail PNG
- `capture-manifest.json`: 이미지 hash, 입력 hash, 모듈별 수량, Scene 무변경, Console 오류를 기록한 manifest
- `actualPlayMode=true`, `actualGameViewCapture=true`
- 모든 모듈 `rendererCount=4`, 전체 `colliderCount=0`
- 전체 mesh vertex 2,250,744개, 동별 최대 132,408개, 단일 batch 최대 82,404개
- 보행망 mesh vertex 193,476개, 관찰 overlay mesh vertex 21,128개
- canonical Scene SHA-256은 실행 전후 모두 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`
- `sceneSaved=false`, `sceneDirtyAfterPlay=false`, 캡처 프로세스 exit code 0

30개 overview와 detail PNG의 SHA-256을 manifest와 다시 대조해 불일치 0건을 확인했다. 소스 index와 manifest의 index SHA-256도 일치한다.

## 한계

캡처 중 기존 canonical Scene의 서버·replay·local endpoint 주변 Console 오류 8건이 기록됐다. 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`다. 캡처와 manifest는 완료됐지만 Console 0, 서버 통합, 이동 가능성 또는 주변 오류 해결을 의미하지 않는다.

보행망은 2020년 자료이고 행정동은 역사 경계 후보이며, 대상 자치구 밖 halo가 완전하지 않다. 보도 폭·경사·계단·연석·출입구·공사·폐쇄·시간대 제한과 현재 통행 상태는 검증하지 않았다. 공개·배포, current pointer, DB·Mongo, Runtime, traversal, gameplay 권위는 모두 부여하지 않았다.
