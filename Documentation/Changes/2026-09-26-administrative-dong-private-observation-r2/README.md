# 30개 행정동 비공개 관찰 오버레이 Game View 증거

이 폴더는 `SimulationWorldShell.unity`를 저장하지 않은 채 실제 Play Mode Game View에서
30개 행정동을 순차 조립한 비공개 검토 증거다. 건물·도로 기초 도형 위에 역사 자료 기반
횡단보도 점 후보 1,533개와 교차로 점 후보 554개를 표시한다.

- 입력 세대: `4D94046D95DAFE6C31A02B484CCE68B8AFB790276197668EF72B5DE879FE3971`
- 입력 index SHA-256: `9C4DC79DFA246105DD8CA7E7A74A6AD7C56A9AC67BABCC687AF4FDBD2BF69A56`
- 출력: 행정동 개요 30장, 면목제3.8동 상세 1장, `capture-manifest.json`
- 실제 실행: Unity `6000.5.6f1`, Play Mode 및 Game View 캡처
- 표현 비용: 모든 동 renderer 4개, collider 0개, 관찰 오버레이 정점 합계 21,128개
- Scene: 실행 전후 SHA-256 동일, Play 종료 후 dirty 상태 없음, 저장 없음

`capture-manifest.json`의 이미지 SHA-256, 모듈 수, 후보 합계, renderer/collider 수를
다시 계산해 확인했다. 집중 EditMode 검사는 18/18 통과했다.

## 해석 경계

이 화면은 2023년 역사 행정동 경계와 공식 원본에서 만든 로컬 비공개 관찰 후보다.
현행 행정동 경계, 정확한 보도 면, 통행 가능성, 도로 폭, 연석, 출입구, 신호 현시,
NPC 이동 또는 운영 상태를 증명하지 않는다. 공개 표시, 배포, current pointer,
runtime 권위, traversal, gameplay 권위는 모두 비활성이다.

실행 중 canonical Scene의 기존 서버 의존 표현에서 콘솔 오류 8건이 기록됐다. 첫 오류는
`SimulationReplayHashMismatch`였고, 나머지 로그에는 로컬 `localhost:5204` 연결 실패와
서버 세션 미설정 오류가 포함됐다. 캡처 절차는 오버레이 수량·해시·메시 예산·Scene
불변 검증을 통과해 exit 0으로 끝났지만, 이 실행은 서버 통합 성공 증거가 아니다.
