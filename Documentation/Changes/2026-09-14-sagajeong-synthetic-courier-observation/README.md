# 사가정 합성 배달 기사 관찰 검증

## 결과

- 기존 사가정 1km 디오라마 외형은 유지하고 `scenario:synthetic-delivery.r1`의 합성 배달 기사 한 명을 runtime-only 표시로 추가했다.
- 기사 표시는 Collider 없이 화면 투영 좌표로 선택하며, 카드는 현재 행동·업무 단계·단계 안 이동 진행·완료/진행 건수와 정산 규칙 미승인을 보여 준다.
- `업무 Actor` 표시를 꺼도 합성 Simulation은 진행한다. 이번 실행에서는 Tick `14 → 15`, Actor X `10 → 15`로 숨김 중 5m 이동한 더 높은 사본을 수용하고, 다시 켰을 때 최신 사본을 표시했다.
- 사본은 `station:kr:kric:s1107:0722`에 결속되며 실제 기사·실제 위치·운영 배차·실제 정산 자료가 아니다.

## Game View

### 전체 조망

![사가정 전체 조망과 업무 Actor 표시](01-overview.png)

### 합성 배달 기사 선택 카드

![합성 배달 기사 선택 카드 근접 조망](02-selected-card-close.png)

## 검증 범위

- 격리 Unity EditMode: 합성 Actor 9건, 공통 역세권 모듈 11건, Mobility Adapter 10건, 사가정 운영 디오라마 7건으로 총 37/37 통과
- 실제 canonical `SimulationWorldShell` Play Mode를 그래픽 프로세스로 실행해 1399×628 Game View 두 장을 새로 캡처
- 원본 프로젝트 `C:/Users/user/ssalddel`과 검증 프로젝트 `C:/Users/user/ssalddel-building-h-validation`을 정규화해 서로 다른 경로임을 검사
- r20 본 트리와 검증 사본의 대상 파일 SHA-256 24/24 일치
- Actor runtime 표시 1개, Collider 0, 프로그램식 화면 좌표 선택 성공
- 선택 전후와 토글·최신 사본 적용 뒤의 Simulation 권위 사본 SHA-256 및 관찰 fingerprint 불변
- Scene SHA-256 실행 전후 `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55` 일치, `sceneDirtyAfterPlay=false`

## 증거 상한과 기존 Scene 경고

- 화면 선택은 검증 조립부가 `TrySelectActorAtScreen`을 직접 호출했다. 실제 사용자 Mouse 입력·uGUI 경합과 production `LifeSimulation` Host 배선은 아직 검증하지 않았다.
- 두 PNG는 정지 화면이다. 숨김 중 위치 변화는 Play Mode manifest의 두 실제 Aggregate Tick 비교로 확인했지만, 실제 도로·차로·신호를 따른 연속 주행을 입증하지 않는다.
- 모의 지급은 `PolicyPending / SimulationSettlementRulePending`이며 금액·통화가 없다. 카드·토글은 주문·배차·정산·Simulation revision을 변경하지 않는다.
- 검증 로그에는 canonical Scene의 기존 누락 Prefab, 로컬 replay hash 불일치, localhost 서버 미기동과 JobTempAlloc 경고가 재현됐다. 이번 절편은 `exit=0`으로 끝났지만 전체 Scene 건전성·live 서버 연결·운영 배달·공개 출시 증거로 확대하지 않는다.
- Scene·Prefab은 저장하지 않았고 E 증거 단계 승격, commit, push, 공개 배포를 수행하지 않았다.

## 파일 SHA-256

- `01-overview.png`: `E0D0458DFC4E8A87F54773D53EDAB929DDE68D0B619CE165DE78E1F0A6CFE6B0`
- `02-selected-card-close.png`: `6D4D510FD5CD16C5369CF54A405B3B89BBB54EBBDC02B7D2AC2EC0D5ABD4AAD7`

원본 실행 manifest와 로그는 로컬 `artifacts/local/validation/sagajeong-synthetic-courier-observation-r20/gameview-20260914-121047/`에 보존한다.
