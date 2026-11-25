# 30개 역사 행정동 차선표시 선형 비공개 Unity 검토 증거

- 상태: `PrivateHistoricalLaneMarkingOutlineGameViewCaptured`
- Unity: `6000.5.6f1`
- 입력 세대: `587601D4E9952149DA299D84496242CAD3CC761F6053D2B36A1A73C5C6B0E3D6`
- index SHA-256: `4DDE2DD68B6246D6DB046FCAB81FA153D111288D887D4C4729B006DFEB6B7AAF`
- exporter SHA-256: `09123CAD89BF1F27DC63C1390725AA661224334E51283F084934EE5F53B3288F`
- 차선표시 wrapper audit SHA-256: `F935FC3FC37E883BAC7D6DB6C9D3DDB60E6FC550E67B8F2A88ECB19E2B6697A3`
- completion SHA-256: `3BF7524435AF29615D8CE1593C8F7BBEE02A8CD07007C55D5BEF0C1E2B9041C4`
- 화면 증거 schema: `administrative-dong-diorama-gameview-evidence.v7`

## 입력과 표현 범위

OA-15537의 2021 파일에 있는 차선표시 선형을 2023 역사 행정동 경계 30개로 자른 비공개 검토 입력이다. 생성기는 공통 WGS84-ECEF-ENU 밀리미터 좌표에서 1mm 이하 선분과 방향을 뒤집어도 같은 물리 선분인 중복을 분리해 감사한 뒤, 표시 가능한 2점 선분만 bundle에 넣었다. 결합 입력은 35개 파일, 123,952,501바이트이며 기존 v1~v5 입력은 변경하지 않았다.

| 항목 | 입력·실제 합계 |
| --- | ---: |
| 표시 후보 | 23,360 |
| fragment | 60,586 |
| 경로점 | 121,172 |
| 원천 절단 선분 | 60,646 |
| 1mm 이하 생략 | 2 |
| 물리 중복 | 58 |
| 표시 선분 | 60,586 |

`60,646 = 60,586 + 2 + 58`을 index, wrapper audit, completion, 30개 bundle과 실제 Unity 증거에서 다시 확인했다. 이 선은 차선표시의 역사적 선형 후보만 나타낸다. 차로, 진행 방향, 통행, 정지선, 신호, 안전 또는 현재 도로 상태 의미로 승격하지 않았다.

## 실제 Game View

canonical `SimulationWorldShell`을 저장하지 않은 채 실제 Play Mode와 Game View에서 30개 overview와 면목제3·8동 detail 1장을 캡처했다. overview는 compact HUD를 사용해 도형을 가리지 않으면서 `차선/방향/신호/보도` 권위가 없음을 명시하고, detail은 전체 근거·깊이 슬롯·Mesh 집계를 유지한다.

- 화면 크기: `765x457`
- PNG 31개 manifest SHA 불일치: 0
- manifest SHA-256: `2D99A95DF1ACD3657740ADB56F51D71D1E8E6E5A1741D4D38EADBA79399CDB1C`
- 면목제3·8동 overview: `modules/20-1126057500-overview.png`, SHA-256 `9889B1AEEB7DF7560736C8A613D1324F131D6DC78B5690441DCA1384B5699A1C`
- 면목제3·8동 detail: `31-1126057500-myeonmok-3-8-detail.png`, SHA-256 `BC3E5EAAB6316E670C7DEB7E552FE07B4B9A5AC656F10BE7A56321A4443B604D`

## Mesh, Scene, 시험

선분마다 같은 폭의 quad를 만들되 새 GameObject, Renderer, MeshFilter 또는 Collider를 추가하지 않고 기존 2x2 결합 Mesh 네 개에 넣었다.

- 차선표시 선형 정점/인덱스: 242,344 / 363,516
- 전체 실제 Mesh 정점/인덱스: 2,935,096 / 4,610,262
- 모듈 최대 정점 194,459, batch 최대 정점 93,328
- 30개 모듈 모두 Renderer 4, MeshFilter 4, Collider 0
- 집중 Unity EditMode 시험 38/38 통과, `editmode-results.xml` SHA-256 `9FB44BDB615431F5A1A9ADBEDEA6EAAD98506CFCD95F13C825305F499A3ED232`
- lane 적용 뒤 v5 또는 기초 도형으로 교체하면 lane overlay와 보고 수치가 0으로 정리됨
- lane 적용 상태에서도 깊이 슬롯 8개, `Mobility=Blocked`, `LaneDirection` 미확인, `laneSignalSidewalkVerified=false`
- canonical Scene SHA-256은 실행 전후 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 동일, Scene 저장 `false`, 종료 시 dirty `false`

실행 중 canonical Scene의 기존 Console 오류 8건과 첫 `SimulationConflictException: SimulationReplayHashMismatch`가 다시 관찰됐다. 이 기록은 차선표시 선형 후보와 Mesh 예산의 실제 화면 증거이며 Console 0, 서버·DB·Mongo 통합 또는 업무 완료 증거가 아니다.

## 권위 경계

`laneOutlineAuthorized=true`는 이 비공개 화면에서 선형 윤곽을 표시할 수 있다는 뜻뿐이다. `laneMeaningAuthorized`, `directionAuthorized`, `trafficAuthorized`, `stopLineAuthorized`, `signalAuthorized`, `publicDisplayAllowed`, `runtimeAuthorized`, `traversalReady`, `gameplayReady`, DB·Mongo 쓰기와 current pointer 사용은 모두 `false`다.
