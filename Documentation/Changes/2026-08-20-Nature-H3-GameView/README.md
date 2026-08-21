# 네이처 H3 Game View 검토 기록

## 목적

`생활핵 → 조우로 → 방어환 → 생활핵` 위치 독립 H3를 실제 Game View에서 살펴보고 다음 공간 설계의 영감을 얻기 위한 비공식 검토 장면이다.

- 장면: `Assets/Ssalddel/Scenes/NatureH3GameViewReview.unity`
- 공식 진입점: 아님
- Build Settings 편입: 안 함
- 서버·Simulation 권위: 없음
- Synty 원본과 `.meta` GUID 변경: 없음

## 조작

- `1`: 전체 조감
- `2`: 생활핵
- `3`: 조우로
- `4`: 방어환
- `5`: 하천·다리·여울
- `F3`: 3인칭
- `F2`: 1인칭
- `WASD`: 표현 전용 플레이어 이동

## 촬영 결과

- `NatureH3-01-Overview.png`: 세 H2와 전체 이동 비율
- `NatureH3-02-HomeCore.png`: 모닥불·통나무·반딧불을 둔 생활핵
- `NatureH3-03-DefenseRing.png`: 바위 경계·횃불·대응 공터
- `NatureH3-04-ForestRiver-Overview.png`: 숲·하천·다리·여울 전체 조감
- `NatureH3-05-RiverCrossing.png`: 다리와 여울을 함께 보는 하천 관점
- `NatureH3-06-BridgeCrossing.png`: 생활핵→조우로 다리 횡단 관점
- `NatureH3-07-FordCrossing.png`: 방어환→생활핵 여울 횡단 관점
- `NatureH3-08-ThirdPersonForest.png`: 플레이어 뒤에서 숲과 하천을 보는 3인칭 관점

Unity 6000.5.6f1 Play Mode에서 촬영했다. 기존 세 PNG는 927×470이고 숲·하천 보강 다섯 PNG는 `Main Camera`를 직접 렌더링한 1280×720 Game View다. 화면은 위치 독립 H3의 구도 검토 증거이고 실제 지역·공공데이터·몬스터 전투·서버 HTTP 완료 증거가 아니다.

## 숲·하천 구조 보강

- Nature 팩 `SM_River_Plane_01`과 원본 `Water_01` Shader Graph를 복제한 표현 재질로 오른쪽 앞→왼쪽 뒤의 굽은 하천을 조립했다. 얕은 지면에서도 수면이 묻히지 않도록 비권위 색상 바닥층을 분리했다.
- 생활핵→조우로에는 다리, 방어환→생활핵에는 여울을 두었다.
- 보이는 물·강변·식생과 이동 차단 Collider를 별도 Root로 분리했다.
- 외곽 숲 24, 강변 나무 8, 하층 식생 20, 바위 16개를 제외 영역과 고정 seed로 배치했다.
- 플레이어는 `CharacterController`로 이동하며 일반 하천 구간은 차단되고 다리·여울 개구부는 열린다.
- 수면은 Game View에서 식별 가능하다. 다만 현재 네 직선 구간을 겹친 초기 경관이라 굴곡부 이음매와 3인칭 플레이어의 T 자세는 후속 시각 마감 대상이다.

## 검증

- Game View 저장 장면 생성: 성공
- 전용 EditMode 시험: 3/3 성공
- 하천 이동 PlayMode 자동 시험: 1/1 성공
- Play Mode 실행과 PNG 3장 생성: 성공
- 숲·하천 보강 Game View PNG 5장 생성: 성공
- 최종 Play Mode Console 오류: 0건
- `2`, `3`, `4`, `F3` 관점 입력 전달: 확인
- canonical `SimulationWorldShell` 변경: 없음
- 숲·하천 보강 실제 Game View 구조: 검증
- 시각 마감: 초기 검토 수준, 후속 필요
- commit·push: 수행하지 않음
