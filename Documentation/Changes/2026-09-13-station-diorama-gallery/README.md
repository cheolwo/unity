# 중랑구 역세권 관찰 디오라마

Unity 저장소의 브랜치 첫 화면에서 현재 역세권 표현 수준을 바로 비교할 수 있도록 사가정역·면목역·용마산역의 대표 Game View를 모았다. 각 이미지를 누르면 원본 크기로 확인할 수 있다.

| 역 | 표현 범위 | 현재 자료 상태 | 대표 화면 |
| --- | --- | --- | --- |
| 사가정역 | 역 기준점 주변 1km × 1km | 기존 사가정 공간 표현 기준선 | [PNG](sagajeong-station-diorama.png) |
| 면목역 | 역 기준점 주변 1km × 1km | `LocalPrivateReview / PartialCoverage`, `MissingCoverage` 2셀 | [PNG](myeonmok-station-diorama.png) |
| 용마산역 | 역 기준점 주변 1km × 1km | `LocalPrivateReview / PartialCoverage`, `MissingCoverage` 10셀 | [PNG](yongmasan-station-diorama.png) |

## 증거 경계

- 세 PNG는 Unity Play Mode의 실제 Game View 대표 화면이다.
- 면목역과 용마산역은 수집 뒤에도 확인되지 않은 범위를 평지나 다른 역 자료로 메우지 않고 `MissingCoverage`로 남긴다.
- 건물·도로·표면은 관찰 표현 입력이다. Collider, NavMesh, 통행 가능성이나 방어 게임 규칙을 확정하지 않는다.
- Unity 화면은 운영 주문·배차·운송 완료의 증거가 아니다. 운영 상태의 최종 권위는 서버 원장과 재조회 결과에 있다.
- 이미지는 개인정보나 실제 기사 위치를 포함하지 않는다.

## 파일 계보

- `sagajeong-station-diorama.png`: 기존 사가정역 Synty 디오라마 대표 Game View 사본
- `myeonmok-station-diorama.png`: 면목역 `myeonmok-station-gameview-r2`의 정리된 대표 Game View
- `yongmasan-station-diorama.png`: 용마산역 `yongmasan-station-gameview-r1`의 최종 대표 Game View

이 갤러리는 README 표현과 검증 기록만 추가하며 Scene·Prefab·서버 상태를 변경하지 않는다.
