# 사가정 음식 배달 이동 Game View 검증

Unity `6000.5.6f1`의 canonical `SimulationWorldShell` Play Mode에서 저장하지 않는 임시 검증 Root로 `SyntheticFixture` 음식 배달 경로를 표시했다. 경로는 Motorcycle/Pedestrian 7구간, 총 116m이며 서버 Command나 운영 주문 상태를 만들지 않는다.

## 실제 이동 관찰

자동 재생을 시작한 뒤 같은 실행을 1.8초 간격으로 읽었다.

| 관찰 | revision | 적용 사본 | 구간 | 모드 | 구간 진행 | 경로 좌표 | 누적 위치 변화 |
| --- | ---: | ---: | ---: | --- | ---: | --- | ---: |
| 시작 | 14 | 10 | 1/7 | `Motorcycle` | 0.269m | `(0.269, 0.000)` | 0.269m |
| 1.8초 뒤 | 377 | 373 | 2/7 | `Pedestrian` | 1.926m | `(10.000, 1.926)` | 11.920m |

두 관찰에서 경로 지문은 `720664785E1CE71ECB7D492ED5903514FA5D1A6237D91896187C99385146B88A`로 같았고 Scene은 dirty가 아니었다. 이 값은 매 프레임 증가하는 합성 상태 사본이 `Interpreter → Playback`을 거쳐 Transform을 갱신한다는 증거다. 서버 API, JSON Decoder, 실제 OSM 길찾기 또는 희소한 서버 사본 사이의 보간 증거는 아니다.

## Game View 사진

- [전체 디오라마](20260913-133053/01-overview.png): 화면 4/7, Motorcycle 15/40m, 전체 보기 표식과 선택 경로
- [오토바이 확대](20260913-133053/02-close-motorcycle.png): 화면 4/7, Motorcycle 35/40m, 기사와 스쿠터가 같은 위치
- [도보 인계 확대](20260913-133053/03-close-pedestrian.png): 화면 5/7, Pedestrian 2/4m, 기사 `(30,22)`와 주차 스쿠터 `(30,20)`가 분리

세 사진은 이동 중 연속 프레임이 아니라 같은 경로의 판독 지점을 비교하기 위한 고정 상태 사본이다. 연속 이동 여부는 위 시간 경과 관찰로 별도 확인했다.

## 검증 경계

- 캡처 완료 로그 1건, 이번 여정 관련 Error/Exception 0건을 확인했다.
- 같은 canonical Scene에서 기존 `SimulationReplayHashMismatch`, 서버 세션 누락, `ConnectionError` 등 범위 밖 Error/Exception 8건은 별도 잔존한다.
- `SimulationWorldShell.unity`의 실행 전후 SHA-256은 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`로 동일했고, 종료 뒤 Play Mode stopped, Scene clean, 임시 Root 0개를 확인했다.
- 실제 OSM 이동망은 `TraversalReady=false`, `RuntimeAuthorized=false`다. 저장 Scene 결속, 실제 입력, Hosted HTTP/live server, 운영 주문·배차 상태 전이와 Save/Replay는 검증하지 않았다.

세부 값과 PNG hash는 [manifest.json](20260913-133053/manifest.json)에 기록했다.
