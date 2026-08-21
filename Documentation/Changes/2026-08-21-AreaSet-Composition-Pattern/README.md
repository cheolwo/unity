# AreaSet 구성 패턴 공용 검토실 기록

## 목적

Hongdal의 역할 슬롯 기반 AreaSet 구성 계획을 Unity의 한 공용 Scene에서 구조와 Synty 표현으로 비교한다.

- 장면: `Assets/Ssalddel/Scenes/AreaSetCompositionPatternReview.unity`
- 대상: Nature·Farm·Town·City/Hub
- 구성: 기준안 4종·변형안 4종
- 공식 진입점: 아님
- Build Settings 편입: 안 함
- 서버·Simulation 권위: 없음

## 조작

- `1~4`: Nature·Farm·Town·City/Hub
- `B` / `V`: 기준안 / 변형안
- `T`: Synty 표현 표시
- `H` / `J`: H3 / H2 경계
- `K`: 연결 지점
- `L`: WI 동선

## 데이터 계보

```text
Hongdal 팩별 Markdown
        ↓
area-set-composition-patterns.v1.json
        ↓ 결정적 검증·hash
area-set-composition-plans.v1.json
        ↓ 원본 영수증과 함께 복제
AreaSetCompositionPatternReview.unity
```

Unity 생성기는 원본 revision과 SHA-256을 영수증에 보존한다. 검토 Scene의 버튼·표시 토글·Synty Prefab은 비권위 표현이며 실제 Session 구성 선택이나 WI 완료를 만들지 않는다.

## 검증

- 연결된 Unity 6000.5.6f1 스크립트 재컴파일: 오류 0건
- 공용 저장 Scene 생성 및 재개방 구조 검증: 성공
- 집중 EditMode 시험: 3/3 성공
- 검증 항목: 8개 패턴 Root, 네 AreaSet, 기준/변형 각 4개, H3/H2 경계, 연결 지점, WI 동선, 실제 Synty 자식, 원본 revision/hash 영수증
- Play Mode·Game View: 미검증
- canonical `SimulationWorldShell`: 변경 없음
- commit·push: 수행하지 않음
