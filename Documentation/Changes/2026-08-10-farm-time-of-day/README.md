# Farm 시간대별 미술 전환 1차 구현

## 결과

`FarmHeroShowcase`에 시간의 흐름을 표현하는 Presentation 전용 시스템을 추가했다. 운영 서버나 Simulation이 업무 상태를 결정하는 경계는 건드리지 않으며, 입력된 정규화 시간만 빛·그림자·환경색·안개·표면 색감으로 해석한다.

## 단계별 적용 범위

1. **TOD0 — 시간 Presentation 계약**
   - `Dawn`, `Morning`, `Midday`, `Afternoon`, `GoldenDusk`, `Night` 여섯 기준 시각을 정의했다.
   - 기준 시각 사이는 `SmoothStep`으로 보간하고 자정 경계도 연속적으로 연결한다.
   - 입력 source는 `FixedReference`, `PreviewScrub`, `SimulationClock`, `OperationalObservation`으로 구분하되 이번 Scene은 고정 미술 기준인 `Midday`를 사용한다.
2. **TOD1 — 조명·환경 적용**
   - 태양 회전·색·광량, soft shadow 강도, 삼색 Ambient, Fog, 카메라 배경색을 하나의 Presentation Model로 적용한다.
   - 기존 Farm Hero의 정오 조명은 회귀 기준으로 그대로 유지한다.
3. **TCS0 — 24개 Composition 표면 측정**
   - Farm 8 family × A/B/C, 총 24개 catalog entry의 renderer와 시간 반응 가능한 material slot을 측정한다.
   - 지면·토양, 작물·잎, 목재, 도로·콘크리트, 지붕, 금속·차량, 유리, 표지·설비의 semantic surface 종류를 분류한다.
4. **TCS1 1차 — 원본 material 비파괴 반응**
   - `MaterialPropertyBlock`으로 Scene instance의 `_BaseColor` 또는 `_Color`만 조절한다.
   - Synty 원본 material과 texture asset은 수정하거나 복제하지 않는다.
   - 현재 단계의 “texture 변화”는 texture 교체가 아니라 같은 texture가 시간대의 색온도·밝기에 반응하는 방식이다.
5. **Play Mode 검증**
   - 동일 카메라에서 여섯 기준 시각을 순차 적용하고 실제 Game View PNG를 캡처했다.

## Game View 증거

| 기준 시각 | 결과 |
| --- | --- |
| 05:30 Dawn | ![Dawn](01-dawn.png) |
| 08:30 Morning | ![Morning](02-morning.png) |
| 12:30 Midday | ![Midday](03-midday.png) |
| 16:00 Afternoon | ![Afternoon](04-afternoon.png) |
| 18:30 Golden Dusk | ![Golden Dusk](05-golden-dusk.png) |
| 21:00 Night | ![Night](06-night.png) |

## 검증

- `FarmHeroShowcaseTests`: 6/6 통과
  - 정오 미술 기준 회귀
  - Dawn/Night 태양·그림자·Ambient·표면 밝기 차이
  - 자정 전후 연속 보간
- `농장풍경CompositionSetTests`: 6/6 통과
  - Farm catalog 24개 완전성
  - 시간 반응 renderer/material slot과 semantic surface 종류 측정
- `FarmCityGraphicalShowcaseTests` + `DioramaCameraTests`: 회귀 8/8 통과
- Unity Editor Play Mode에서 여섯 기준 시각을 같은 Game View로 순차 확인했다.

## 다음 단계

- TCS1 확장: 24개 Composition preview를 한 Scene에 놓고 시간별 contact sheet를 생성한다.
- TCS2: surface 종류별 반응 계수로 흙·잎·목재·금속의 밝기와 색온도 차이를 분리한다.
- TCS3: Town/City catalog에도 같은 inventory와 adapter를 연결한다.
- TOD2 이후: 밤 창문·가로등 emissive, 데이터 강조색 보호, 모바일 GPU profiling을 별도 Gate로 진행한다.
