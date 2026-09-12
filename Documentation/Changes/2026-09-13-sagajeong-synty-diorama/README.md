# 사가정 Synty 혼합 디오라마 1차 구현

## 결과

- canonical `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`의 사가정 관찰 화면에서 동결 지도 좌표를 유지한 채 도로 위계, 보도·연석, 건물 높이군, 접지 그림자와 Synty 건물 표현을 조립했다.
- 표현 판본은 `sagajeong-synty-visual.r1`이다. 전체 후보 120동 중 90동을 Synty Prefab으로 표현하고, 맞지 않는 30동은 기존 외곽 Mesh로 유지한다.
- 모든 Synty Prefab은 하나의 `Synty VisualRoot · 표현 전용` 아래에만 존재한다. Collider를 제거하고 관찰 카메라 전용 layer에 두어 운영 권위·통행·방어 판정에 영향을 주지 않는다.
- `SagajeongReference.json`의 건물 602개·도로 선분 2,397개와 지도 hash는 변경하지 않았다.

## 실제 Game View

사가정역 확대:

![사가정역 확대 Game View](sagajeong-station-gameview.png)

전체 1km 보기:

![사가정 전체 Game View](sagajeong-whole-gameview.png)

두 이미지는 Unity 6000.5.6f1 Play Mode에서 `사가정 운영 관찰 카메라`를 직접 렌더링한 결과다. 생성 콘셉트는 구도·깊이·밀도 목표일 뿐 실제 건물 외형·높이·시설 위치의 근거가 아니다.

## 검증

- 컴파일 오류 0.
- `도로명주소표현Tests` 21/21.
- `건물높이보완Tests` 19/19.
- `역방어준비Tests` 8/8.
- `운영관찰WorldTests` 5/5.
- `사가정운영디오라마Tests` 7/7.
- 최종 `사가정Synty표현Tests` 필터 7/7.
- Play Mode 10초 측정: 3,898표본, 평균 237.4fps, 95백분위 frame time 5.41ms. 기준인 PC Editor 30fps를 넘었다.

Console에는 이번 표현 코드 오류는 없었다. canonical Scene의 다른 모듈이 로컬 서버 세션 없이 시작하면서 내는 기존 연결 오류 9건(`*ServerSessionMissing`, `ConnectionError`)은 남아 있다. 따라서 이 기록은 **사가정 표현의 실제 Play Mode·Game View 검증 완료**이며, 전체 Scene의 서버 연결 정상이나 Console 0건을 뜻하지 않는다.

## 남은 범위

- Synty 4종 후보는 형태 근사치다. 실제 건물 외형의 동일성이나 입주·용도 사실을 만들지 않는다.
- 식재·횡단보도·역사 출입구는 승인된 배치 자료가 없어 추가하지 않았다.
- 실제 서버 연결, 플레이어 입력 완주, 방어 폐루프 E 승격은 이번 시각 절편의 범위가 아니다.
