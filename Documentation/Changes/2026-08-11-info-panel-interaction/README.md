# 2026-08-11 공통 정보 Panel 상호작용

감자 생산·유통 연구 Scene 9개와 `신티에셋연구소`의 주요 정보 Panel에 공통 UI 상태를 추가했다.

- `펼침`: 정보 Panel과 `접기`·`닫기` 버튼을 표시한다.
- `접힘`: 본문을 숨기고 같은 위치에 `<Panel 이름> 펼치기` 탭을 남긴다.
- `닫힘`: 본문을 숨기고 원래 Panel 하단에 `<Panel 이름> 다시 열기` 탭을 남긴다.
- UI 상태는 Presentation 전용이며 감자 lifecycle, 수량, stable ID, Simulation·서버 권위를 변경하지 않는다.

적용 Scene:

1. `감자농장출발단계구현`
2. `감자도시도착단계구현`
3. `감자물류거점입고검수흐름`
4. `감자물류거점판로분배흐름`
5. `감자수확포장상차흐름`
6. `감자재배수확흐름`
7. `생산자온라인직판흐름`
8. `생산자조합인수흐름`
9. `수확물판로선택`
10. `신티에셋연구소`

Scene 관리는 `CityFarmWorld/감자생산유통`, `CityFarmWorld/생산자판로`,
`CityFarmWorld/에셋연구`의 세 맥락 폴더로 나눈다. 파일명과 `.meta` GUID는 유지한다.

검증:

- Unity 6000.5.6f1 script recompile: 오류 0건
- 공통 상태 전이 EditMode test: 1/1 통과
- 9개 업무 Scene Builder 생성과 자체 wiring 검증 통과
- 10개 Scene asset 모두 `정보Panel상호작용Controller` 직렬화 연결 확인
- 12개 관련 Scene을 맥락별 폴더로 이동하고 기존 `.meta` GUID 보존 test 12/12 통과
- `신티에셋연구소` Play Mode, Console 새 오류 0건, 1600×900 Game View 확인

`신티에셋연구소` 전체 Builder는 기존 catalog 재생성 뒤 Presenter wiring 검증에서 중단되어, 이번에는 기존 Scene을 열어 공통 Panel만 좁게 적용했다. 감자 농장 Scene의 `ScreenSpaceOverlay` UI는 Pipeline 카메라 캡처에 포함되지 않아 hierarchy와 직렬화 연결로 보완 확인했다.

![신티 에셋 연구소 정보 Panel 펼침 상태](info-panel-expanded.png)
