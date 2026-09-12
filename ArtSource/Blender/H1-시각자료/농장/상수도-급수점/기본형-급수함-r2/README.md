# 농장 상수도 급수점 H1 — 개방형 호스 소켓 r2

![농장 상수도 급수점 r2](농장-상수도-급수점-H1-r2.png)

## 채택 범위

- H1 역할: `농장 상수도 급수점`
- 제안 식별자: `h1-proposal:farm-pressurized-water-service-point`
- r1의 급수함 외형은 H1 시안으로 채택됐다.
- r2는 파란 연결구의 막힌 마개를 개방형 호스 소켓으로 바꾼 후속 검토본이다.
- 모델 크기: 약 `1.25×1.03×1.11m`

## 연결 기준점

- `Anchor_WaterMainIn`: 지하 상수관 입력
- `Anchor_HoseOut`: 개방형 소켓 중심의 압력 호스 출력
- `Anchor_Service`: 플레이어 또는 NPC의 밸브·점검 상호작용

후속 관수 표현은 `상수도 급수점 H1 → 호스 H1 → 2.5×2.5m 경작 구획 H1`로 조립한다. 호스가 연결되지 않은 상태와 연결·관수 중 상태를 분리하며, 급수함 H1의 FBX에는 호스를 고정 포함하지 않는다.

## 파일과 경계

- Blender: `농장-상수도-급수점-H1-r2.blend`
- 프로젝트 소유 FBX: `농장-상수도-급수점-H1-r2.fbx`
- 검토 이미지: `농장-상수도-급수점-H1-r2.png`
- 검증 결과: `검증결과-r2.json`

현재 r2 외형은 `Presentation E4 visual candidate / PendingUserReview`다. Unity Import·Prefab·Collider·World 배치·호스 변형·물 효과·Game View·E5는 성립하지 않는다.
