# 숲 경계 생활농장 방어 H4 — 한스 AreaSet r4

검증된 r3 배치 초안을 원본으로 삼아 재사용 가능한 `h4-blueprint:forest-edge-living-farm-defense-region`의 첫 한스 인스턴스를 검토하는 Blender 마스터 장면이다.

## 장면 구성

- `H3_생활생산`: 생활주택·작업마당·상차점·경작 구획
- `H3_접근방어`: 진입로·문·울타리·숲 완충·주 방어 집결점
- `H3_피해회복복귀`: 조건부 울타리 돌파점·후퇴 회복점
- `Normal`: 경로를 숨긴 평상시 조감
- `Defense`: 주 침입로, 플레이어·한스, 보급·수리 경로
- `BreachRecovery`: 울타리 손상 때만 열릴 돌파 경로와 후퇴·회복 경로

파란 반투명 상자는 정확 자산이 승인되지 않은 H1 자리표시자다. 기존 r3의 31개 배치 객체는 Synty 원본을 수정하지 않고 그대로 재사용한다.

## 결과

- `forest-edge-living-farm-defense-h4-r4.blend`: H4/H3 컬렉션과 세 View Layer를 가진 마스터 장면
- `h4-normal-overview.png`: 평상시 조감
- `h4-defense-overview.png`: 방어 경로 조감
- `h4-breach-recovery-overview.png`: 돌파·회복 경로 조감
- `h4-master-receipt.json`: 생성 결과와 원본 hash
- `h4-master-validation.json`: 재개방 구조 검사 결과

## 증거 상한

이 폴더는 Presentation E4 저작·검토 자료다. canonical Unity Scene, 실제 조건부 경로 실행, Renderer·Collider·Bounds·입력·결과 결속과 E5를 증명하지 않는다. Blender에서는 수평 위치·Yaw·균일 배율만 저작 대상으로 보며 실제 지면 접지는 Unity가 원본 지형에서 다시 계산해야 한다.
