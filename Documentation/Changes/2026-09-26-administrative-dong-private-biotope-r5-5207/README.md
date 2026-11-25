# 30개 역사 행정동 비오톱 비공개 Unity 검토 증거

- 상태: `PrivateHistoricalBiotopeOutlineGameViewCaptured`
- Unity: `6000.5.6f1`
- 입력 세대: `5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E`
- index SHA-256: `951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6`
- exporter SHA-256: `6D376489BFD03E0A5D32F561C54F905A341BD0D9492C76A2D132967CEB5F71E9`
- 비오톱 audit SHA-256: `416F57C447B0C543FFC5258927A0CE5B339FD4B654A70D14A3209025626D5CA5`
- 방향표시 audit SHA-256: `D5AEFD2562878DF10D802736C99841D9504E0BB62DFB292C3020859927E8C8D1`
- completion SHA-256: `6B82872C282D7FEF93A0BECE8E16FA4290E478B9675DDD18A604D9D0F2115C0F`

## 실제 화면 증거

canonical `SimulationWorldShell`을 저장하지 않은 채 실제 Play Mode와 Game View에서 30개 행정동 overview와 면목제3·8동 detail 1장을 캡처했다. 화면 크기는 `765x457`이고 PNG 31개의 파일 SHA-256을 `capture-manifest.json`과 다시 대조해 불일치 0개를 확인했다.

- manifest SHA-256: `CC17D40708D5D159665389B0153E4315CAF3BB8C20D9722069C0E7592D5C4089`
- 면목제3·8동 overview: `modules/20-1126057500-overview.png`, SHA-256 `FA031833107D305EB6A5F7C264E0C85AADAA6A84E6C805E0C8A52740F4691E2D`
- 면목제3·8동 detail: `31-1126057500-myeonmok-3-8-detail.png`, SHA-256 `45A6E8F362C25E94590502B5B17968FE77900FBEF76C26ECA8CB82871BA61EF1`

## 비오톱 표현 결과

2025 비오톱 polygon을 2023 역사 행정동 경계로 자른 로컬 후보만 사용했다. 채운 면이나 새 GameObject를 만들지 않고 외곽 ring과 내부 ring을 선분 quad로 기존 2x2 결합 Mesh에 넣었다.

| 항목 | 실제 합계 |
| --- | ---: |
| 비오톱 후보 | 3,181 |
| polygon part | 3,408 |
| 내부 ring | 108 |
| ring 점 | 105,731 |
| 원본 선분 | 102,215 |
| 1mm 이하 생략 선분 | 128 |
| 표시 선분 | 102,087 |
| 비오톱 정점 / 인덱스 | 408,348 / 612,522 |

Unity Mono와 일반 Python/.NET의 JSON `0.0` 내용 hash 정규화 차이를 생성 단계에서 재현하고, 실제 Unity Mono 규칙으로 index·audit·completion·30개 bundle·30개 비오톱 overlay를 다시 검산했다. 이전 v1~v4 자료는 보존했다.

방향표시 후보 8,415개는 계속 방향 미해석 마름모이고 화살표는 0개다. 방향표시 정점은 33,660개다. `DRN` 각도축·회전 방향·단위와 `LENX` 단위는 여전히 확인되지 않았다.

## Mesh·Scene·시험

- 30개 모듈 모두 Renderer 4, MeshFilter 4, Collider 0
- 전체 실제 Mesh 정점 2,692,752, 인덱스 4,246,746
- 모듈 최대 정점 176,695, batch 최대 정점 88,944
- 집중 Unity EditMode 시험 32/32 통과, 결과 XML SHA-256 `77BC7AA14E37C4519AE284FCBCCE8D21754F1B1E4136B732FB7E8F2E920A18E8`
- canonical Scene SHA-256은 실행 전후 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 동일
- Scene 저장 `false`, 종료 시 dirty `false`

실행 중 canonical Scene의 기존 Console 오류 8건을 다시 관찰했다. 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`다. 이 기록은 비오톱 외곽선 표현과 Mesh 예산의 실제 화면 증거이며 Console 0, 서버·DB·Mongo 통합 또는 업무 완료 증거가 아니다.

## 권위 경계

현재 생태 상태, 생물종·개체 관찰, 법적 효력, 안전, 통행, 현행 행정동 coverage를 확정하지 않는다. `currentPointerUsed=false`, `distributionApproved=false`, `runtimeAuthorized=false`, `traversalReady=false`, `gameplayReady=false`를 유지한다. 공개 표시, DB·Mongo 쓰기, 채운 polygon, Collider, Scene·Prefab 저장은 수행하지 않았다.
