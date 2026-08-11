# 통합 모판·전시관 EXH-2

City·Farm·Town Pack을 하나의 3/4 전시 공간에 배치하고, 서버가 정의한 전시 manifest를 Unity가 읽기 전용으로 해석하는 첫 Scene을 구성했다.

- 왼쪽 자료관은 공공데이터와 canonical 관계를 설명한다.
- 가운데 농장은 감자 재배·수확 Simulation 계약을 보여 준다.
- 오른쪽 모판은 Farm·Town·City 에셋 연구 후보를 모은다.
- 각 전시는 `DataState`, `ExperienceMode`, 완료 상태, 권한 범위와 코드·집중 test·Runtime·운영 증거를 독립 표시한다.
- 현실 관측은 실제 값을 수집하지 않았으므로 `미수집·읽기 전시·차단`으로 표시한다.
- generic Confirm이나 운영 Command는 제공하지 않는다.

Play Mode에서 `감자 현실 관측`을 선택해 실제 관측 미수집과 운영 증거 미확인 경계를 확인했다. Unity 6000.5.6f1 재컴파일 오류 0건, Scene Builder 검증, 집중 EditMode 3/3을 통과했고 1600×900 Game View를 캡처했다.

![실제 관측 미수집 상태의 통합 모판 전시관](integrated-seedbed-exhibition.png)
