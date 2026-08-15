# 농장 경영 기본 3인칭과 선택 가능한 1인칭

## 결과

`SimulationWorldShell`의 농장 진입 기본 시점을 높은 사선의 RTS형 3인칭으로 두고, 기존 농장 1인칭 전환을 그대로 유지했다.

```text
농장 경영 진입
├─ 기본: 전술 3인칭
│  ├─ 농지 10개와 수확 마당 선택
│  ├─ Shift+좌클릭 다중 선택
│  ├─ 1~4 밭갈기·파종·관수·수확 선택
│  └─ 우클릭 위치 기반 작업 초안
└─ 선택: 1인칭
   └─ WASD 직접 이동과 마우스 시선

탐험 진입
└─ 기본: 1인칭

시점 전환
├─ 기본 0.9초 ease-in-out
├─ 높이 1.8m의 완만한 이차 곡선 이동
├─ 위치·회전·화각 연속 보간
├─ 전환 중 이동·선택·경영 입력 잠금
└─ 도중 반대 전환 시 현재 화면 위치에서 다시 연결
```

작업 초안은 화면에서 선택 관계를 준비할 뿐 실제 Simulation 작업을 확정하지 않는다. `RequiresExplicitConfirm=true`, `ChangesWorldState=false`, `PresentationOnly=true`이며 후속 서버 Preview와 명시적 Confirm 연결이 필요하다.

## 검증

- 공통 활동별 시점 정책 시험 3/3 통과
- Presentation·Editor·EditMode 정적 C# build 오류 0개
- 저장된 Scene 기반 농장 경영·곡선 계산 EditMode 3/3 통과
- 통합 화면 버튼·전환 완료 PlayMode 1/1 통과
- Unity 배치 실행으로 `SimulationWorldShell` 재배선·저장 성공

연결된 Unity Editor가 없어 실제 Game View PNG와 손으로 누른 입력 검증은 이번 변경에서 생성하지 않았다. Scene View나 자동 시험을 Game View 증거로 대체하지 않는다.
