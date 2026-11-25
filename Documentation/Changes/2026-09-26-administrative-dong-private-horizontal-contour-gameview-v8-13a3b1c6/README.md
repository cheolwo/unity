# 30개 역사 행정동 평면 등고선 추적 비공개 Unity 검토 증거

- 상태: `PrivateHistoricalFlatHorizontalContourTraceGameViewCaptured`
- Unity: `6000.5.6f1`
- 입력 세대: `13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753`
- index SHA-256: `C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9`
- exporter SHA-256: `168721AABE97B3B2B6C64985500ABDAC5E1B779E8DB104C0C302C13EAC031E2B`
- 평면 등고선 wrapper audit SHA-256: `D8F2EBFFF77418BAA05396D0EF13E8222B1FBD4A0126A366E90159C7F303B65D`
- completion SHA-256: `26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D`
- 화면 증거 schema: `administrative-dong-diorama-gameview-evidence.v8`

## 입력과 표현 범위

OA-22241의 `file-2025-03-20` 수평 선형을 2023 역사 행정동 경계 30개로 자른 비공개 검토 입력이다. 원천 속성 DB와 높이·표고값은 읽거나 직렬화하지 않았다. 후보에는 stable ID와 공통 WGS84-ECEF-ENU 밀리미터 좌표의 2점 선분만 들어 있다. 1mm 이하 선분은 분리해 감사하고 표시하지 않았다. 결합 입력은 36개 파일, 179,363,531바이트이며 기존 v1~v6 입력 계약은 계속 읽을 수 있다.

| 항목 | 입력·실제 합계 |
| --- | ---: |
| 표시 후보 | 860 |
| fragment | 184,747 |
| 경로점 | 369,494 |
| 원천 절단 선분 | 184,748 |
| 1mm 이하 생략 | 1 |
| 물리 중복 | 0 |
| 표시 선분 | 184,747 |

`184,748 = 184,747 + 1 + 0`을 index, wrapper audit, completion, 30개 bundle과 실제 Unity 증거에서 다시 확인했다. 이 선은 원천의 수평 형상 흔적만 나타낸다. 등고선 의미, 높이, 표고, 수직 기준면, 지형 표면, 이동 가능성 또는 현재 경계 의미로 승격하지 않았다.

## 실제 Game View

canonical `SimulationWorldShell`을 저장하지 않은 채 실제 Play Mode와 Game View에서 30개 overview와 면목제3·8동 detail 1장을 캡처했다. 31장 모두 `765x457`이고 비어 있지 않으며, 파일 SHA-256이 manifest와 일치하고 서로 중복되지 않는다. overview compact HUD와 detail full HUD에는 평면 선형 수와 `높이값 미읽기`, `표고/지형/통행 권위 없음`이 표시된다.

- manifest SHA-256: `E7312015B116096F42DD9CDA054CF81C043BC66249F5DBB48C97CAC58689767D`
- 최대 등고선 동: `region:kr:hjd:1121577000` 서울특별시 광진구 중곡제4동, 표시 선분 32,652
- 중곡제4동 overview: `modules/04-1121577000-overview.png`
- 면목제3·8동 overview: `modules/20-1126057500-overview.png`, SHA-256 `415510DCD74F7BDA0DA73823C74A83AFE5EC0616BC0AD3F30F1D3438B8F913A0`
- 면목제3·8동 detail: `31-1126057500-myeonmok-3-8-detail.png`, SHA-256 `D429D5F6FB73F11855D760755380ED50AD20C5C141EB03F4F2B3293266D8682F`

중곡제4동 overview, 면목제3·8동 overview와 detail을 직접 확인했다. 주요 HUD 문구가 화면 밖으로 잘리지 않고, 평면 등고선 추적이 기존 건물·도로·관찰 레이어와 함께 보인다.

## Mesh, Scene, 시험

선분마다 고정된 평면 높이의 quad를 만들되 새 GameObject, Renderer, MeshFilter 또는 Collider를 추가하지 않고 기존 2x2 결합 Mesh 네 개에 넣었다.

- 평면 등고선 정점/인덱스: 738,988 / 1,108,482
- 전체 실제 Mesh 정점/인덱스: 3,674,084 / 5,718,744
- 모듈 최대 정점: 288,423
- batch 최대 정점: 114,556 / 허용 500,000
- 30개 모듈 모두 Renderer 4, MeshFilter 4, Collider 0
- 집중 Unity EditMode 시험 44/44 통과, `editmode-results.xml` SHA-256 `D06292BFAC0FEBA807066443E759C1B66A9503A440B649AF01C6B0ACE646C0AD`
- v1~v6 입력은 contour 상태를 남기지 않고, v7 적용 뒤 구세대 또는 기초 도형으로 교체해도 overlay와 보고 수치가 정리되는 회귀를 확인
- contour가 있어도 깊이 슬롯 8개, `Mobility=Blocked`, `Environment=NotProvided`, `LaneDirection` 미확인 상태를 유지
- canonical Scene SHA-256은 실행 전후와 현재 파일 모두 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 동일하며 Git diff 없음, Scene 저장 `false`, 종료 시 dirty `false`

실행 중 기존 서버 세션이 없는 검토 환경의 Console 오류 8건이 다시 관찰됐다. 첫 오류는 `SimulationConflictException: SimulationReplayHashMismatch`이며 이전 차선표시 v7 캡처도 같은 8건과 같은 첫 오류를 기록했다. 같은 로그에는 `TurnClosingServerRequestFailed:0:ConnectionError`, `WorldTileStreamRequestFailed:0:ConnectionError` 등이 있다. 행정동 캡처는 31장을 끝까지 만들었지만, 이 증거를 Console 0 또는 서버 통합 완료로 해석하지 않는다.

## 권위 경계

`flatHorizontalTraceOnly=true`와 `sourceHorizontalGeometryObserved=true`는 비공개 화면에서 수평 선형을 그렸다는 뜻뿐이다. `sourceAttributeDatabaseRead`, `sourceElevationValuesRead`, `sourceElevationValuesSerialized`, `currentBoundaryEstablished`, `currentTerrainEstablished`, `contourMeaningAuthorized`, `elevationMeaningAuthorized`, `heightUnitVerified`, `verticalDatumVerified`, `terrainSurfaceAuthorized`, `terrainMeshAuthorized`, `publicDisplayAllowed`, `runtimeAuthorized`, `traversalReady`, `gameplayReady`, DB·Mongo 쓰기와 current pointer 사용은 모두 `false`다.
