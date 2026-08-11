# 에셋연구-0 · 신티 에셋 연구소

## 결과

- Farm 498개, Town 702개, City 335개, 합계 1,535개 Prefab을 GUID 기반 `에셋원본Index`로 자동 색인했다.
- 자동 원본 목록과 사람이 작성하는 `에셋연구Catalog`를 분리했다.
- 묶음·분류별 12개 표본을 `3열 × 4행`으로 전시하고 이전 쪽·다음 쪽·묶음·분류 전환을 제공한다.
- 첫 연구 항목 `온실 01`에 관찰된 사실, 현실 의미, 월드 역할 후보, 함께 둘 에셋, 연결할 자료 후보와 승격 후보를 기록했다.
- 화면 이름은 한국어 중심으로 정리하고 원본 Synty 이름은 출처 사실로만 함께 표시한다.

## 권위 경계

- Synty 원본 Prefab은 수정하지 않는다.
- 표본은 `VisualRoot/SyntyPrefabInstance` 아래에서만 표현한다.
- 선택은 카메라 초점·받침 강조·연구 카드만 바꾸며 Command, Tick, 실제 저장이나 운영 API를 실행하지 않는다.
- `farm.facility.greenhouse`는 연구 단계의 승격 후보이며 아직 Domain·Simulation 권위가 아니다.

## 검증

- 연구소 집중 EditMode 4개: 최종 상태에서 각각 통과
- Unity Play Mode: `신티에셋연구소` Scene 실행 확인
- Game View: 1600×900, 한글 카드와 12개 표본 확인
- Console: 최종 Scene 생성·Play Mode에서 오류 0건
- 전체 EditMode 병행 실행: 36/47 통과, 기존 `Assets/Ssalddel/Experiments - 연구/...` 경로를 가정한 무관한 테스트 11개가 `Experiments - 연구` 폴더명 변경으로 실패

## 대표 화면

![신티 에셋 연구소 온실 연구 카드](신티에셋연구소-온실.png)
