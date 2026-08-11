# 연구 Scene 한국어 이름 정리

## 결과

`Assets/Ssalddel/Experiments - 연구` 아래 영어 Scene 26개를 한국어 목적 이름으로 바꿨다. 기존 `신티에셋연구소`를 포함해 연구 Scene 27개 모두 Project 창에서 장소와 흐름을 한국어로 파악할 수 있다. Unity `AssetDatabase`로 이름을 바꿔 기존 GUID는 유지했다.

## 이름 대응

| 이전 이름 | 한국어 이름 |
| --- | --- |
| CityFarmBusinessViewIntegration | 농장도시업무화면통합 |
| CityFarmCargoJourney | 농장도시화물이동 |
| CityFarmMacroWorldBlockout | 농장도시공간배치초안 |
| CityFarmSyntyWorldPrototype | 농장도시신티월드시제품 |
| CityFarmVisualQualityGate | 농장도시시각품질검증 |
| AnchorCompositionLibraryPreview | 거점조합모음미리보기 |
| CommonAnimationPreview | 공용동작미리보기 |
| FarmCompositionSetLibraryPreview | 농장풍경조합모음미리보기 |
| RoadGateCompositionLibraryPreview | 도로출입구조합모음미리보기 |
| CooperativeIntakeLifecycle | 생산자조합인수흐름 |
| DirectOnlineSaleLifecycle | 생산자온라인직판흐름 |
| FarmCityGraphicalShowcase | 농장도시그래픽전시 |
| FarmHeroShowcase | 농장대표풍경전시 |
| HarvestDispositionChoice | 수확물판로선택 |
| PotatoCargoJourneyLifecycle | 감자화물전체이동흐름 |
| PotatoCultivationLifecycle | 감자재배수확흐름 |
| PotatoHarvestCargoLifecycle | 감자수확포장상차흐름 |
| PotatoHubDispositionLifecycle | 감자물류거점판로분배흐름 |
| PotatoHubReceivingLifecycle | 감자물류거점입고검수흐름 |
| PotatoJourneyCityVerticalSlice | 감자도시도착단계구현 |
| PotatoJourneyFarmVerticalSlice | 감자농장출발단계구현 |
| PotatoJourneyHubRoute | 감자농장물류거점이동 |
| ThreeRegionHubJourney | 농장마을도시물류거점이동 |
| UrbanLogisticsCityPackVerticalSlice | 도심물류센터도시팩적용연구 |
| UrbanMarketCityPackVerticalSlice | 도심마트도시팩적용연구 |
| SyntyCommunityPlazaExperiment | 신티커뮤니티광장연구 |

## 검증

- 한국어 이름 검증: 2/2 통과
- 27개 연구 Scene 전체 열기, root 존재와 파일명·Scene 이름 일치 확인
- Unity 기본 EditMode assembly: 55/55 통과
- 통합 EditMode assembly: 114/115 통과
- 남은 1건은 `HarvestDispositionChoiceViewTests`가 선택지 3개를 기대하지만 현재 구현은 4개인 기존 불일치이며 Scene 이름 변경과 무관하다
- 최종 Unity 스크립트 재컴파일: `failed=false`, 컴파일 오류 0건
- Console에는 앞선 병렬 Test Runner가 상태 파일을 동시에 쓰며 남긴 sharing violation 기록이 있으나 이번 이름 변경의 컴파일 오류는 아니다

## 화면 변경 기록

화면 없음. Scene 내부 오브젝트, 카메라, 조명, 재질과 렌더 결과는 변경하지 않았고 파일명과 경로 참조만 정리했다.
