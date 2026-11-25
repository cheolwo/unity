# 30개 역사 행정동 비오톱 검토 화면 가독성 증거

- 상태: `CompactOverviewHudGameViewCaptured`
- Unity: `6000.5.6f1`
- 입력 세대: `5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E`
- index SHA-256: `951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6`
- 화면 증거 schema: `administrative-dong-diorama-gameview-evidence.v6`
- manifest SHA-256: `609D723B171216B7D6F8F040DDDED0128AAC8287831B7AF8FEC8E694ECC21B3B`

## 조망과 상세 화면 분리

30개 행정동 overview에서는 304px 상단 근거 패널과 116px 하단 집계 패널 대신 82px의 `CompactSourceAndCount` 패널만 표시한다. 행정동 이름, 비공개·배포 불가 상태, 건물·도로·보행 노드·방향 후보·비오톱 수, 2023 역사 경계와 권위 결손을 남기고 도형을 가리는 면적을 줄였다.

같은 650px 폭에서 overview 패널 사각형 합은 273,000px²에서 53,300px²로 80.48% 줄었다. 하단 패널은 overview에서 제거하고 상세 증거에만 유지한다.

면목제3·8동 detail은 `FullEvidenceAndSelection`을 유지해 전체 자료 근거·깊이 슬롯·선택 건물·Mesh 집계를 확인할 수 있다. 표현 모드만 분리했으며 입력 bundle, 도형, 카메라 선택 규칙, Mesh와 권위 플래그는 바꾸지 않았다.

- 실제 Play Mode·Game View: overview 30장 + detail 1장
- 화면 크기: `765x457`
- PNG 31개 manifest hash 불일치: 0
- 면목제3·8동 overview: `modules/20-1126057500-overview.png`, SHA-256 `249B781DB2817D5EA53540EBFC7B33E3BB6BFB3177AF545615D87A45860F46F8`
- 면목제3·8동 detail: `31-1126057500-myeonmok-3-8-detail.png`, SHA-256 `99248B055E186273684AC633801404A0072873C371320F733CE4CB75FD4E2D7A`

## 불변 자료·Mesh·시험

- 비오톱 후보/part/내부 ring: 3,181 / 3,408 / 108
- 비오톱 정점/인덱스: 408,348 / 612,522
- 방향 후보/미해석/화살표: 8,415 / 8,415 / 0
- 전체 Mesh 정점/인덱스: 2,692,752 / 4,246,746
- 모든 모듈 Renderer 4, MeshFilter 4, Collider 0
- 집중 Unity EditMode 시험: 32/32, XML SHA-256 `B0B56DF340202D4AA1F9CB2CD7556A67F5A063CCC8E9F4ED892AA07EAE087B9A`
- canonical Scene SHA-256 실행 전후 동일, 저장 `false`, 종료 뒤 dirty `false`

실행 중 기존 Console 오류 8건과 첫 `SimulationConflictException: SimulationReplayHashMismatch`는 다시 관찰됐다. 현재성·주소·필지·통행·차선·신호·생태·법적 의미, 공개·DB·Mongo·current·Runtime·Traversal·Gameplay 권위는 성립하지 않는다.
