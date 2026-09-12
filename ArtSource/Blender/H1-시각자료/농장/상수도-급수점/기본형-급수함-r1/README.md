# 농장 상수도 급수점 H1 — 기본형 급수함 r1

![농장 상수도 급수점 검토 이미지](농장-상수도-급수점-H1-r1.png)

## 역할

- 제안 식별자: `h1-proposal:farm-pressurized-water-service-point`
- 표시명: `농장 상수도 급수점`
- 모델 크기: 약 `1.25×1.02×1.11m`
- 역할: 지하 상수관의 물을 차단 밸브와 호스 출수구를 통해 농장 작업에 공급한다.
- 제외: 배수·하수 처리, 정수 처리, 실제 유량 계산

## 연결 기준점

- `Anchor_WaterMainIn`: 지하 상수관 입력
- `Anchor_HoseOut`: 압력 호스 출력
- `Anchor_Service`: 플레이어 또는 NPC의 밸브·점검 상호작용

## 파일

- Blender: `농장-상수도-급수점-H1-r1.blend`
- 프로젝트 소유 FBX: `농장-상수도-급수점-H1-r1.fbx`
- 검토 이미지: `농장-상수도-급수점-H1-r1.png`
- 검증 결과: `검증결과-r1.json`
- 재생성 스크립트: `ArtSource/Blender/source/farm-water-service-point/build_water_service_point.py`

Synty City 배관 부품과 Town 호스 릴은 비례·색감 참고 자료로만 사용했다. 이 모델은 Blender 기본 도형으로 만든 프로젝트 소유 후보이며 공급사 원본을 수정하거나 포함하지 않는다.

## 증거 경계

현재 상태는 `Presentation E4 visual candidate / PendingUserReview`다. Unity Import·Prefab·Collider·World 배치·호스 연결·유량·Game View·E5는 성립하지 않는다.
