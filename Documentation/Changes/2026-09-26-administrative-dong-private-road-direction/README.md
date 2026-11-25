# 30개 역사 행정동 방향표시 비공개 Unity 검토 증거

이 폴더는 서울시 방향표시 관련 정보 OA-15536의 2026-09-10 원본을 역사 행정동 경계에 귀속한 **비공개 표현 검토** 결과다. 현행 행정동 정본, 차로 방향, 통행 허가, 런타임 이동 권위 또는 공개 배포 자료가 아니다. 방향 각도 `DRN`과 길이 `LENX`의 의미·단위가 확정되지 않았으므로 화살표로 해석하지 않고 고정 크기 무방향 마름모만 표시했다.

## 결속 입력

- generation: `3A7475C181E6BE11D46F256B40150119B95C3473477F529FDA7802B51053CD33`
- index SHA-256: `6170476FEEB84A4584E4C2D4EE5B3EA9B6CF651BEF12FF39E21780C4F020E887`
- exporter source SHA-256: `87B4D35EF30EC629D7F43BBE982A88C5E2F82D11B1A6BFC37A3E3DFACB2674E8`
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동

## 실제 Unity 실행 증거

- Unity: `6000.5.6f1`
- 실제 Play Mode: `true`
- 실제 Game View 캡처: `true`
- 모듈 overview: 30장
- 면목3·8동 detail: 1장
- 방향표시 후보: 8,415개
- 의미 미해석 후보: 8,415개
- 렌더 화살표: 0개
- 방향표시 추가 정점: 33,660개
- 전체 모듈 mesh 정점: 2,284,404개
- 모듈 최대 정점: 133,156개
- batch 최대 정점: 82,792개
- 모듈별 renderer: 4개
- 모듈별 mesh filter: 4개
- 전체 Collider: 0개

`capture-manifest.json`은 각 이미지의 SHA-256, 동별 수량, renderer·mesh·Collider 수, 실제 Play/Game View 여부를 함께 보존한다. 31개 PNG의 파일 hash는 캡처 뒤 독립 재검산해 모두 일치했다.

## Scene 및 시험 경계

- canonical Scene 전 SHA-256: `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`
- canonical Scene 후 SHA-256: `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`
- Scene 저장: `false`
- Play 종료 뒤 Scene dirty: `false`
- EditMode 집중 시험: 26/26 통과
- 시험 결과 XML SHA-256: `1D3E813703A7AC4CF08859E579BAFB7AA479BC3B5258B3C6D608E516500440BE`

캡처 중 기존 canonical Scene에서 Console 오류 8개가 관찰됐고 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`였다. 방향표시 overlay 생성·mesh 예산·캡처는 완료됐지만, 이 실행을 Console 0 또는 서버·DB·Mongo·current pointer·traversal·gameplay 통합 증거로 사용하지 않는다.

정밀 좌표와 원천 결속 산출물은 비공개 로컬 검토 범위에만 남긴다. 공개 표시, 현재성 승인, 통행 방향 확정, 운영 수명주기 권위는 모두 차단 상태다.
