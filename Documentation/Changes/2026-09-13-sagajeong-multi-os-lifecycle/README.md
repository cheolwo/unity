# 사가정 네 OS 생명주기 Game View 검증

사가정 1km 디오라마 위에 음식배달·국내화물·창고·마트 네 OS의 정상/회복 표본 8개와 로컬 검토용 음식점 아이콘 12개를 읽기 전용으로 합성했다. 같은 표식은 단계가 바뀔 때 검증 전용 일반화 기준점 사이를 1.2초 `SmoothStep`으로 이동한다.

![네 OS 전체 보기](01-overview-all-os.png)

![음식배달 회복 근접 보기](02-food-recovery.png)

![창고·화물·마트 인계 근접 보기](03-warehouse-cargo-mart-handoff.png)

## 실행 증거

- Unity 6000.5.6f1 비배치 Play Mode 캡처가 정상 종료 코드 0으로 끝났다.
- EditMode 집중 시험은 10/10 통과했다.
- 화면 순간별 표시 수는 전체 8, 음식배달 회복 2, 최종 인계 근접 8이다.
- 음식점 입력은 `sagajeong-restaurant-icon-fixture.v1`, 12곳·샘플 메뉴 30개이며 SHA-256은 `f253fed02190c42c1548301f3638adb7abfb8318ab4a5f1fb812ed69d9578a4a`다.
- 실제 Play Mode 중간 프레임에서 Food 96.830, Cargo 67.231, Warehouse 56.639, Mart 43.278 Unity XZ 단위의 위치 변화가 관측됐다. 각 중간 표본은 전체 이동거리의 47.5~50.4% 지점이다.
- `SimulationWorldShell.unity`와 `UrbanMarketManagerPrimitive.unity`는 저장하지 않았고 실행 전후 SHA-256이 각각 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55`, `EBAF69C047FD76A20A8BAD7DC1E85AC62EB992EE87ED74405FF0EE78B9DD5FFD`로 유지됐다.

## 증거 경계

- 재생 원천은 `FrozenPresentationTimeline`이며 `liveHttpEndToEnd=false`다. 같은 날 별도로 수행한 서버 HTTP 검증을 이 캡처의 live 연결 증거로 해석하지 않는다.
- 이동 방식은 `GeneralizedStageAnchorInterpolation`이고 `routeAuthority=false`다. 실제 도로·차선·신호·길찾기 또는 운영 업무 완료를 증명하지 않는다.
- 음식점 명칭과 메뉴는 `LocalPrivateReview`·`SyntheticFixture` 결속이며 실제 판매 메뉴, 제휴, 주문 가능 상태가 아니다. 공개·운영 효과는 비활성이다.
- 정지 PNG는 프레임 간 연속 이동 자체를 증명하지 않는다. 해당 증거는 로컬 검증 manifest의 실제 Play Mode 프레임 표본에 있다.
- 기존 Scene 기준선에는 누락 Prefab 56 GUID, Unknown script 10건, SearchDatabase 예외 1건, 로컬 WorldTile 연결 실패 1건, 종료 시 JobTempAlloc 경고 2건이 남아 있다.
