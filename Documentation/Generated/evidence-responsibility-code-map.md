# Unity E 책임 코드 지도

> C# E 책임 Attribute에서 자동 생성된다. 직접 수정하지 않는다.

- 후보 타입: `457`
- 책임 지정: `320`
- 사유 있는 제외: `137`
- 미분류: `0`

| E | G | 모듈 | 대표 | 보조 |
| --- | --- | --- | ---: | ---: |
| `E1` | `G1` | `CoreContractModule` 핵심 계약 | 0 | 0 |
| `E2` | `G1` | `RuntimeBoundaryModule` 실행 경계 | 84 | 0 |
| `E3` | `G1` | `RegressionEvidenceModule` 회귀 증거 | 63 | 0 |
| `E4` | `G1` | `InteractionSpatialBindingModule` 상호작용 공간 결속 | 0 | 0 |
| `E5` | `G1` | `ActualWorldAssemblyModule` 실제 세계 조립 | 110 | 0 |
| `E6` | `G1` | `WorldRefinementModule` 세계 정제 | 0 | 0 |
| `E7` | `G2` | `PlayableExperienceLoopModule` 플레이 경험 폐루프 | 59 | 0 |
| `E8` | `G3` | `LifeContinuityModule` 생활 연속성 | 4 | 0 |
| `E9` | `G4` | `ChangeEnvelopeModule` 변화 봉투 | 0 | 0 |

## 구성 요소

| 구성 요소 | 대표 E | 상태 | 소스 |
| --- | --- | --- | --- |
| `Ssalddel.Unity.Application.Bootstrap.WorldBootstrapUseCase` | `E7` | `Annotated` | `Assets/Ssalddel/Application/Bootstrap/WorldBootstrapUseCase.cs` |
| `Ssalddel.Unity.Application.Exhibition.마트피킹포장WorldLoadUseCase` | `E7` | `Annotated` | `Assets/Ssalddel/Application/Exhibition/마트피킹포장WorldLoadUseCase.cs` |
| `Ssalddel.Unity.Application.Exhibition.통합전시관SimulationLoadUseCase` | `E7` | `Annotated` | `Assets/Ssalddel/Application/Exhibition/통합전시관SimulationLoadUseCase.cs` |
| `Ssalddel.Unity.Application.WorldMap.LoadPublicWorldMapUseCase` | `E7` | `Annotated` | `Assets/Ssalddel/Application/WorldMap/LoadPublicWorldMapUseCase.cs` |
| `Ssalddel.Unity.Bootstrap.Nature생존AutoBootstrap` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/Nature생존AutoBootstrap.cs` |
| `Ssalddel.Unity.Bootstrap.WorldBootstrapSceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/WorldBootstrapSceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.공간TileStreamingCompositionRoot` | `E5` | `Annotated` | `Assets/Ssalddel/Bootstrap/공간TileStreamingCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.네이처탐험조우CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/네이처탐험조우CompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.농장전투CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/농장전투CompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.대관령L2창고아이템CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/대관령L2창고아이템CompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.물류이동SceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/물류이동SceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.오늘작업계획SceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/오늘작업계획SceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.정착지상호작용SceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/정착지상호작용SceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.진부Hub입고UiSceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/진부Hub입고UiSceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.카드서랍CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/카드서랍CompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.턴마감SceneCompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/턴마감SceneCompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.팀원관전CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/팀원관전CompositionRoot.cs` |
| `Ssalddel.Unity.Bootstrap.현장전투CompositionRoot` | `E2` | `Annotated` | `Assets/Ssalddel/Bootstrap/현장전투CompositionRoot.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.CityFarmBusinessViewIntegrationBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/CityFarmBusinessViewIntegrationBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.CityFarmCargoJourneyBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/CityFarmCargoJourneyBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.CityFarmVisualQualityGateBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/CityFarmVisualQualityGateBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.CooperativeIntakeLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/CooperativeIntakeLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.DirectOnlineSaleLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/DirectOnlineSaleLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.EveningHakdangBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/EveningHakdangBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.FarmCityGraphicalShowcaseBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/FarmCityGraphicalShowcaseBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.FarmHeroShowcaseBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/FarmHeroShowcaseBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.FarmProductVisualCatalogBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/FarmProductVisualCatalogBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.HarvestDispositionChoiceBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/HarvestDispositionChoiceBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoCargoJourneyLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoCargoJourneyLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoCultivationLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoCultivationLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoHarvestCargoLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoHarvestCargoLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoHubDispositionLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoHubDispositionLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoHubReceivingLifecycleBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoHubReceivingLifecycleBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoJourneyCityBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoJourneyCityBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoJourneyFarmVerticalSliceBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoJourneyFarmVerticalSliceBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.PotatoJourneyHubRouteBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/PotatoJourneyHubRouteBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.감자생산유통통합Builder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/감자생산유통통합Builder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.세RegionHubJourneyBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/세RegionHubJourneyBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.신티에셋연구소Builder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/신티에셋연구소Builder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.연구Scene폴더정리Builder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/연구Scene폴더정리Builder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.정보Panel대상SceneBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/정보Panel대상SceneBuilder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.정보Panel상호작용Builder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/정보Panel상호작용Builder.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.CityFarmBusinessViewIntegrationTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/CityFarmBusinessViewIntegrationTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.CityFarmCargoJourneyTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/CityFarmCargoJourneyTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.CityFarmVisualQualityGateTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/CityFarmVisualQualityGateTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.CooperativeIntakeLifecycleViewTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/CooperativeIntakeLifecycleViewTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.DirectOnlineSaleLifecycleViewTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/DirectOnlineSaleLifecycleViewTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.EveningHakdangViewTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/EveningHakdangViewTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.FarmCityGraphicalShowcaseTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/FarmCityGraphicalShowcaseTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.FarmHeroShowcaseTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/FarmHeroShowcaseTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.FarmProductVisualCatalogTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/FarmProductVisualCatalogTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.HarvestDispositionChoiceViewTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/HarvestDispositionChoiceViewTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoCargoJourneyLifecycleTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoCargoJourneyLifecycleTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoCultivationLifecycleTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoCultivationLifecycleTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoHarvestCargoLifecycleTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoHarvestCargoLifecycleTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoHubDispositionLifecycleTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoHubDispositionLifecycleTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoHubReceivingLifecycleTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoHubReceivingLifecycleTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoJourneyCityTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoJourneyCityTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoJourneyFarmVerticalSliceTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoJourneyFarmVerticalSliceTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoJourneyHubRouteTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoJourneyHubRouteTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoJourneyOperationalApiClientTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoJourneyOperationalApiClientTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.PotatoProductionDistributionBootstrapCatalogTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/PotatoProductionDistributionBootstrapCatalogTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.SyntyAnimationSourceInventoryTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/SyntyAnimationSourceInventoryTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.SyntyCompositionSourceMeasurementTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/SyntyCompositionSourceMeasurementTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.감자생산유통통합Tests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/감자생산유통통합Tests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.거점CompositionSetTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/거점CompositionSetTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.공용AnimationAdapterTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/공용AnimationAdapterTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.도로GateCompositionSetTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/도로GateCompositionSetTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.세RegionHubJourneyTests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/세RegionHubJourneyTests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests.신티에셋연구소Tests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/신티에셋연구소Tests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Tests.Editor.연구Scene폴더정리Tests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/연구Scene폴더정리Tests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Tests.Editor.정보Panel상호작용Tests` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Tests/Editor/정보Panel상호작용Tests.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.CargoJourneyAnchorView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/CargoJourneyAnchorView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.CargoJourneyView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/CargoJourneyView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.CooperativeIntakeLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/CooperativeIntakeLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.DirectOnlineSaleLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/DirectOnlineSaleLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.EveningHakdangPresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/EveningHakdangPresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.HarvestDispositionChoicePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/HarvestDispositionChoicePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.OperationalPotatoJourneyApiClient` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/OperationalPotatoJourneyApiClient.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoCargoJourneyLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoCargoJourneyLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoCultivationLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoCultivationLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoHarvestCargoLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoHarvestCargoLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoHubDispositionLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoHubDispositionLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoHubReceivingLifecyclePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoHubReceivingLifecyclePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoJourneyCityPresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoJourneyCityPresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoJourneyFarmSlicePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoJourneyFarmSlicePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoJourneyHubRoutePresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoJourneyHubRoutePresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.PotatoProductionDistributionBootstrapPresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/PotatoProductionDistributionBootstrapPresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.WorldBusinessPresentationCoordinator` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/WorldPresentationFallbackView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.WorldPresentationFallbackView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/WorldPresentationFallbackView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.WorldSelectionEvidenceView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/WorldPresentationFallbackView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.WorldVisualQualityGateView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/WorldVisualQualityGateView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.감자생산유통통합Presenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/감자생산유통통합Presenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.농장환경SwayPresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/농장환경SwayPresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.세RegionHubJourneyView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/세RegionHubJourneyView.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.야간MonsterRaidPresenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/야간MonsterRaidPresenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.에셋연구소Presenter` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/에셋연구소Presenter.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.에셋연구표본View` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/에셋연구표본View.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.정보Panel상호작용Controller` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/정보Panel상호작용Controller.cs` |
| `Ssalddel.Unity.Experiments.CityFarmWorldIntegration.화물PresentationJourneyView` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Runtime/화물PresentationJourneyView.cs` |
| `Ssalddel.Unity.Editor.AreaSet구성패턴검토실Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/AreaSet구성패턴검토실Builder.cs` |
| `Ssalddel.Unity.Editor.CityFarmMacroWorldBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/CityFarmMacroWorldBuilder.cs` |
| `Ssalddel.Unity.Editor.CityFarmSyntyWorldBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/CityFarmSyntyWorldBuilder.cs` |
| `Ssalddel.Unity.Editor.H2공간조합검토RootBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/H2공간조합검토RootBuilder.cs` |
| `Ssalddel.Unity.Editor.H계층실증ShellBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/H계층실증ShellBuilder.cs` |
| `Ssalddel.Unity.Editor.SimulationWorldShellBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/SimulationWorldShellBuilder.cs` |
| `Ssalddel.Unity.Editor.SyntyPackAssetInventoryBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/SyntyPackAssetInventoryBuilder.cs` |
| `Ssalddel.Unity.Editor.UnityEvidenceResponsibilityMapBuilder` | `E3` | `Annotated` | `Assets/Ssalddel/Editor/UnityEvidenceResponsibilityMapBuilder.cs` |
| `Ssalddel.Unity.Editor.WI공간모판검토실Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/WI공간모판검토실Builder.cs` |
| `Ssalddel.Unity.Editor.WorldBootstrapSceneBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/WorldBootstrapSceneBuilder.cs` |
| `Ssalddel.Unity.Editor.거점CompositionSetBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/거점CompositionSetBuilder.cs` |
| `Ssalddel.Unity.Editor.공간TileStreamingBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/공간TileStreamingBuilder.cs` |
| `Ssalddel.Unity.Editor.공간문법CompositionCatalogBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/공간문법CompositionCatalogBuilder.cs` |
| `Ssalddel.Unity.Editor.공간문법SyntyBindingCatalogBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/공간문법SyntyBindingCatalogBuilder.cs` |
| `Ssalddel.Unity.Editor.공용AnimationPreviewBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/공용AnimationPreviewBuilder.cs` |
| `Ssalddel.Unity.Editor.네이처H3GameViewSceneBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/네이처H3GameViewSceneBuilder.cs` |
| `Ssalddel.Unity.Editor.네이처생활핵조우방어SceneBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/네이처생활핵조우방어SceneBuilder.cs` |
| `Ssalddel.Unity.Editor.네이처탐험조우SceneBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/네이처탐험조우SceneBuilder.cs` |
| `Ssalddel.Unity.Editor.농장풍경CompositionSetBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/농장풍경CompositionSetBuilder.cs` |
| `Ssalddel.Unity.Editor.대한민국법정동WorldBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/대한민국법정동WorldBuilder.cs` |
| `Ssalddel.Unity.Editor.도로GateCompositionSetBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/도로GateCompositionSetBuilder.cs` |
| `Ssalddel.Unity.Editor.실제E5AreaSetNetworkShellBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/실제E5AreaSetNetworkShellBuilder.cs` |
| `Ssalddel.Unity.Editor.자연경관CompositionSetBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/자연경관CompositionSetBuilder.cs` |
| `Ssalddel.Unity.Editor.자연경관EventOverlayBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/자연경관EventOverlayBuilder.cs` |
| `Ssalddel.Unity.Editor.정적경관배치ReviewService` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/정적경관배치ReviewService.cs` |
| `Ssalddel.Unity.Editor.턴카드모판Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/턴카드모판Builder.cs` |
| `Ssalddel.Unity.Editor.통합SimulationWorldBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/통합SimulationWorldBuilder.cs` |
| `Ssalddel.Unity.Editor.통합WorldScenePolicy` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/통합WorldScenePolicy.cs` |
| `Ssalddel.Unity.Editor.통합월드자유이동Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/통합월드자유이동Builder.cs` |
| `Ssalddel.Unity.Editor.통합전시관Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/통합전시관Builder.cs` |
| `Ssalddel.Unity.Editor.통합전시관Object모판Builder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/통합전시관Object모판Builder.cs` |
| `Ssalddel.Unity.Editor.팩경관CompositionSetBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Editor/팩경관CompositionSetBuilder.cs` |
| `Ssalddel.Unity.Samples.Farm.Editor.FarmPrimitiveSceneBuilder` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Editor/FarmPrimitiveSceneBuilder.cs` |
| `Ssalddel.Unity.Samples.Farm.Tests.Editor.FarmSoilTileGridViewTests` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Tests/Editor/FarmSoilTileGridViewTests.cs` |
| `Ssalddel.Unity.Samples.Farm.CropView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/CropView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmSceneController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmSceneController.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmSoilTileActionButtonView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmSoilTileActionButtonView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmSoilTileCellView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmSoilTileCellView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmSoilTileGridView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmSoilTileGridView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmSoilTileSimulationController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmSoilTileSimulationController.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmTileView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmTileView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmView.cs` |
| `Ssalddel.Unity.Samples.Farm.FarmWorkerView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmWorkerView.cs` |
| `Ssalddel.Unity.Samples.Farm.OperationalFarmProducerApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmApiClients.cs` |
| `Ssalddel.Unity.Samples.Farm.SensorView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/SensorView.cs` |
| `Ssalddel.Unity.Samples.Farm.SimulatedFarmProducerApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Farm Primitive Vertical Slice/Runtime/FarmApiClients.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.AreaSet접근ServerRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/AreaSet접근ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.Farm창고건설ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/Farm창고건설ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.Farm플레이어배치ServerRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/Farm플레이어배치ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.Hosted비콘ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/Hosted비콘ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.ISimulationUnifiedBattleAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/SimulationUnifiedBattleServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.LocalFarmChoiceAuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/LocalSessionGameplayAuthorityRepositories.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.LocalLogisticsMovementAuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/LocalSessionGameplayAuthorityRepositories.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.LocalSimulationUnityRuntime` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/LocalSimulationUnityRuntime.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.LocalTurnClosingAuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/LocalSessionGameplayAuthorityRepositories.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.Nature생존CoreRuntimeAdapter` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/Nature생존CoreRuntimeAdapter.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.Nature심상ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/Nature심상ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.SimulationFarmCombatServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/SimulationFarmCombatServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.SimulationUnifiedBattleServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/SimulationUnifiedBattleServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.공간TileStreamServerRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/공간TileStreamServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.네이처탐험조우ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/네이처탐험조우ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.대관령L2창고아이템ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/대관령L2창고아이템ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.물류이동AuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/물류이동AuthorityRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.오늘작업계획ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/오늘작업계획ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.정착지상호작용AuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/정착지상호작용AuthorityRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.진부Hub입고UiServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/진부Hub입고UiServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.턴마감ServerAuthorityRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/턴마감ServerAuthorityRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.통합전시관SimulationSessionRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/통합전시관SimulationSessionRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.팀역할CardServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/팀역할CardServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.팀원관전ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/팀원관전ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Simulation.협동창고ServerRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Simulation/협동창고ServerRepository.cs` |
| `Ssalddel.Unity.Infrastructure.Transport.OperationalUnityWebRequestApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Transport/UnityWebRequestApiClient.cs` |
| `Ssalddel.Unity.Infrastructure.Transport.SimulationRehearsalUnityWebRequestApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Transport/UnityWebRequestApiClient.cs` |
| `Ssalddel.Unity.Infrastructure.Transport.UnityWebRequestApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/Transport/UnityWebRequestApiClient.cs` |
| `Ssalddel.Unity.Infrastructure.UrbanMarket.OperationalMarketOrderIntentApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/UrbanMarket/OperationalMarketSeedbedApiClients.cs` |
| `Ssalddel.Unity.Infrastructure.UrbanMarket.OperationalMarketPickingPackingWorldRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/UrbanMarket/OperationalMarketPickingPackingWorldRepository.cs` |
| `Ssalddel.Unity.Infrastructure.UrbanMarket.OperationalMarketProductApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/UrbanMarket/OperationalMarketSeedbedApiClients.cs` |
| `Ssalddel.Unity.Infrastructure.WorldMap.CommunityWorldMapRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Infrastructure/WorldMap/CommunityWorldMapRepository.cs` |
| `Ssalddel.Unity.Samples.NpcMovement.CargoWarehouseHandoffView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/NPC Movement Primitive Vertical Slice/Runtime/CargoWarehouseHandoffView.cs` |
| `Ssalddel.Unity.Samples.NpcMovement.NpcMovementView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/NPC Movement Primitive Vertical Slice/Runtime/NpcMovementView.cs` |
| `Ssalddel.Unity.Samples.NpcMovement.NpcWaypointView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/NPC Movement Primitive Vertical Slice/Runtime/NpcWaypointView.cs` |
| `Ssalddel.Unity.Samples.NpcMovement.ZoneNpcMovementController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/NPC Movement Primitive Vertical Slice/Runtime/ZoneNpcMovementController.cs` |
| `Ssalddel.Unity.Presentation.Bootstrap.WorldBootstrapPresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/Bootstrap/WorldBootstrapPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.DioramaForegroundOcclusionController` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/DioramaForegroundOcclusionController.cs` |
| `Ssalddel.Unity.Presentation.World.DioramaOcclusionView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/DioramaForegroundOcclusionController.cs` |
| `Ssalddel.Unity.Presentation.World.Farm플레이어배치Controller` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/Farm플레이어배치Controller.cs` |
| `Ssalddel.Unity.Presentation.World.Farm플레이어배치PalettePresenter` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/Farm플레이어배치PalettePresenter.cs` |
| `Ssalddel.Unity.Presentation.World.Nature생존Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/Nature생존Controller.cs` |
| `Ssalddel.Unity.Presentation.World.Npc업무행동Presenter` | `E8` | `Annotated` | `Assets/Ssalddel/Presentation/World/Npc업무행동Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.Npc업무행동View` | `E8` | `Annotated` | `Assets/Ssalddel/Presentation/World/Npc업무행동View.cs` |
| `Ssalddel.Unity.Presentation.World.SimulationWorldDistrictView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/SimulationWorldDistrictView.cs` |
| `Ssalddel.Unity.Presentation.World.SimulationWorldNavigationTargetView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/SimulationWorldNavigationTargetView.cs` |
| `Ssalddel.Unity.Presentation.World.SimulationWorldShellPresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/SimulationWorldShellPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판CandidateView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판CapacityView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판ConnectorView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판RelationView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판SpaceView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.WI공간모판검토Presenter` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/WI공간모판검토Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.WorldVisualInstanceView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/WorldVisualInstanceView.cs` |
| `Ssalddel.Unity.Presentation.World.거점CompositionSetView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/거점CompositionSetView.cs` |
| `Ssalddel.Unity.Presentation.World.경관품질PipelineView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/경관품질PipelineView.cs` |
| `Ssalddel.Unity.Presentation.World.공간PhysicalElevationMeshBuilder` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간PhysicalElevationMeshBuilder.cs` |
| `Ssalddel.Unity.Presentation.World.공간StreamingTreeDiagnosticPresenter` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간StreamingTreeDiagnosticPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.공간TileStreamingController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간TileStreamingController.cs` |
| `Ssalddel.Unity.Presentation.World.공간WorldPipelineView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간WorldPipelineView.cs` |
| `Ssalddel.Unity.Presentation.World.공간문법PlacementInstanceView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간문법LandscapeRuntimeAssembler.cs` |
| `Ssalddel.Unity.Presentation.World.공간시야ObjectStreamingController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/공간시야ObjectStreamingController.cs` |
| `Ssalddel.Unity.Presentation.World.공급망MacroWorldView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/공급망MacroWorldView.cs` |
| `Ssalddel.Unity.Presentation.World.공급망WorldRouteView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/공급망WorldRouteView.cs` |
| `Ssalddel.Unity.Presentation.World.공급망WorldZoneView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/공급망WorldZoneView.cs` |
| `Ssalddel.Unity.Presentation.World.공용AnimationAdapter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/공용AnimationAdapter.cs` |
| `Ssalddel.Unity.Presentation.World.네이처조우Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/네이처조우Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.농장경영선택대상View` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/농장경영시점Controller.cs` |
| `Ssalddel.Unity.Presentation.World.농장경영시점Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/농장경영시점Controller.cs` |
| `Ssalddel.Unity.Presentation.World.농장풍경CompositionAdapter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/농장풍경CompositionAdapter.cs` |
| `Ssalddel.Unity.Presentation.World.농장풍경CompositionSetView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/농장풍경CompositionSetView.cs` |
| `Ssalddel.Unity.Presentation.World.농장풍경CompositionSocketView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/농장풍경CompositionSocketView.cs` |
| `Ssalddel.Unity.Presentation.World.단일PlayerViewController` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/단일PlayerViewController.cs` |
| `Ssalddel.Unity.Presentation.World.대관령L2창고상호작용TargetView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/일인칭창고상호작용Controller.cs` |
| `Ssalddel.Unity.Presentation.World.대관령L2창고아이템Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/대관령L2창고아이템Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.도로GateCompositionSetView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/도로GateCompositionSetView.cs` |
| `Ssalddel.Unity.Presentation.World.몰입경관InstanceController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/몰입경관InstanceController.cs` |
| `Ssalddel.Unity.Presentation.World.물류이동Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/물류이동Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.법정동WorldProjectionView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/법정동WorldProjectionView.cs` |
| `Ssalddel.Unity.Presentation.World.법정동경관VisualInstanceView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/법정동경관VisualCatalog.cs` |
| `Ssalddel.Unity.Presentation.World.법정동화물운송View` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/법정동화물운송View.cs` |
| `Ssalddel.Unity.Presentation.World.실제E5AreaSetNetworkController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/실제E5AreaSetNetworkController.cs` |
| `Ssalddel.Unity.Presentation.World.실제E5AreaSetNetworkHudPresenter` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/실제E5AreaSetNetworkHudPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.역할CharacterVisualInstanceView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/역할CharacterVisualInstanceView.cs` |
| `Ssalddel.Unity.Presentation.World.오늘작업계획Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/오늘작업계획Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.월드CompositionContractValidator` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/월드CompositionContract.cs` |
| `Ssalddel.Unity.Presentation.World.월드시간대Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/월드시간대Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.일인칭경관CameraController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/일인칭경관CameraController.cs` |
| `Ssalddel.Unity.Presentation.World.일인칭창고상호작용Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/일인칭창고상호작용Controller.cs` |
| `Ssalddel.Unity.Presentation.World.자연경관CompositionSetView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/자연경관CompositionSetView.cs` |
| `Ssalddel.Unity.Presentation.World.자연경관EventOverlayController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/자연경관EventOverlayController.cs` |
| `Ssalddel.Unity.Presentation.World.자연경관EventOverlayView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/자연경관EventOverlayView.cs` |
| `Ssalddel.Unity.Presentation.World.자연경관SeasonPresentationController` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/자연경관SeasonPresentationController.cs` |
| `Ssalddel.Unity.Presentation.World.자연경관ShadowPolicyView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/자연경관ShadowPolicyView.cs` |
| `Ssalddel.Unity.Presentation.World.전략카메라Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전략카메라Controller.cs` |
| `Ssalddel.Unity.Presentation.World.전술분대Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전술분대Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.전술분대대형Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전술분대대형Controller.cs` |
| `Ssalddel.Unity.Presentation.World.전술분대원View` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전술분대원View.cs` |
| `Ssalddel.Unity.Presentation.World.전투시점Controller` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전투시점Controller.cs` |
| `Ssalddel.Unity.Presentation.World.전투입력Adapter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/전투입력Adapter.cs` |
| `Ssalddel.Unity.Presentation.World.정적경관배치AnchorView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/정적경관배치AnchorView.cs` |
| `Ssalddel.Unity.Presentation.World.정적경관배치InstanceView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/정적경관배치InstanceView.cs` |
| `Ssalddel.Unity.Presentation.World.정적경관배치ReceiptView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/정적경관배치ReceiptView.cs` |
| `Ssalddel.Unity.Presentation.World.정착지상호작용Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/정착지상호작용Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.진부Hub입고UiPresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/진부Hub입고UiPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.카드서랍Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/카드서랍Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.타로객체강조Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/타로객체강조Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.턴마감Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/턴마감Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.턴카드모판Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/턴카드모판Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.통합월드ModePresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/통합월드ModePresenter.cs` |
| `Ssalddel.Unity.Presentation.World.통합전시관ObjectPreviewPresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/통합전시관ObjectPreviewPresenter.cs` |
| `Ssalddel.Unity.Presentation.World.통합전시관Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/통합전시관Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.통합전시관ScenePlacementView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/통합전시관ScenePlacementView.cs` |
| `Ssalddel.Unity.Presentation.World.통합전시관SimulationController` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/통합전시관SimulationController.cs` |
| `Ssalddel.Unity.Presentation.World.팀원관전CameraController` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/팀원관전CameraController.cs` |
| `Ssalddel.Unity.Presentation.World.팀원관전Coordinator` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/팀원관전Coordinator.cs` |
| `Ssalddel.Unity.Presentation.World.팀원관전PosePresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/팀원관전PosePresenter.cs` |
| `Ssalddel.Unity.Presentation.World.팀원관전자표시Presenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/World/팀원관전자표시Presenter.cs` |
| `Ssalddel.Unity.Presentation.World.팩경관CompositionSetView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/팩경관CompositionSetView.cs` |
| `Ssalddel.Unity.Presentation.World.평창4Pack경관ProfileView` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/평창4Pack경관Profile.cs` |
| `Ssalddel.Unity.Presentation.World.플레이어경관Controller` | `E5` | `Annotated` | `Assets/Ssalddel/Presentation/World/플레이어경관Controller.cs` |
| `Ssalddel.Unity.Presentation.WorldMap.PublicWorldMapPresenter` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/WorldMap/PublicWorldMapPresenter.cs` |
| `Ssalddel.Unity.Presentation.WorldMap.PublicWorldMapSceneController` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/WorldMap/PublicWorldMapSceneController.cs` |
| `Ssalddel.Unity.Presentation.WorldMap.PublicWorldMapSceneView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/WorldMap/PublicWorldMapSceneView.cs` |
| `Ssalddel.Unity.Presentation.WorldMap.PublicWorldMarkerView` | `E7` | `Annotated` | `Assets/Ssalddel/Presentation/WorldMap/PublicWorldMarkerView.cs` |
| `Ssalddel.Unity.Samples.ResidentialPickup.ResidentialPickupPointView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Residential Pickup Primitive Vertical Slice/Runtime/ResidentialPickupPointView.cs` |
| `Ssalddel.Unity.Samples.ResidentialPickup.ResidentialPickupView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Residential Pickup Primitive Vertical Slice/Runtime/ResidentialPickupView.cs` |
| `Ssalddel.Unity.Runtime.Exhibition.I통합전시관SimulationSessionRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Exhibition/통합전시관SimulationSessionModels.cs` |
| `Ssalddel.Unity.Runtime.ExhibitionFixtures.통합전시관FixtureApiModelFactory` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Exhibition/통합전시관FixtureApiModelFactory.cs` |
| `Ssalddel.Unity.Runtime.Identity.IUnitySessionRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Identity/UnitySessionModels.cs` |
| `Ssalddel.Unity.Runtime.Ledgers.IUnityLedgerProjectionRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Ledgers/UnityLedgerProjection.cs` |
| `Ssalddel.Unity.Runtime.Transport.IOperationalUnityApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Transport/UnityApiTransport.cs` |
| `Ssalddel.Unity.Runtime.Transport.ISimulationRehearsalUnityApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Transport/UnityApiTransport.cs` |
| `Ssalddel.Unity.Runtime.Transport.IUnityApiClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/Transport/UnityApiTransport.cs` |
| `Ssalddel.Unity.Runtime.World.AreaSet접근Coordinator` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/AreaSet접근Models.cs` |
| `Ssalddel.Unity.Runtime.World.Farm선택Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm선택Models.cs` |
| `Ssalddel.Unity.Runtime.World.Farm창고건설Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm창고건설Models.cs` |
| `Ssalddel.Unity.Runtime.World.Farm플레이어배치Coordinator` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm플레이어배치Models.cs` |
| `Ssalddel.Unity.Runtime.World.H5세계배치LoadingService` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/H5세계배치Streaming.cs` |
| `Ssalddel.Unity.Runtime.World.Hosted비콘Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Hosted비콘Models.cs` |
| `Ssalddel.Unity.Runtime.World.IAreaSet접근AuthorityClient` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/AreaSet접근Models.cs` |
| `Ssalddel.Unity.Runtime.World.IFarm선택AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm선택Models.cs` |
| `Ssalddel.Unity.Runtime.World.IFarm창고건설AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm창고건설Models.cs` |
| `Ssalddel.Unity.Runtime.World.IFarm플레이어배치AuthorityClient` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/Farm플레이어배치Models.cs` |
| `Ssalddel.Unity.Runtime.World.IH5세계배치Repository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/H5세계배치Streaming.cs` |
| `Ssalddel.Unity.Runtime.World.IHosted비콘AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Hosted비콘Models.cs` |
| `Ssalddel.Unity.Runtime.World.INature심상AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Nature심상Models.cs` |
| `Ssalddel.Unity.Runtime.World.I공간AreaSetLandscapeGraphRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간LandscapeGraphStreaming.cs` |
| `Ssalddel.Unity.Runtime.World.I공간LHWorldRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간LHWorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.I공간TileLandscapeCompositionRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간LandscapeCompositionModels.cs` |
| `Ssalddel.Unity.Runtime.World.I공간TileStreamRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간TileStreamingModels.cs` |
| `Ssalddel.Unity.Runtime.World.I네이처탐험조우AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/네이처탐험조우Models.cs` |
| `Ssalddel.Unity.Runtime.World.I대관령L2창고아이템AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/대관령L2창고아이템Models.cs` |
| `Ssalddel.Unity.Runtime.World.I마트피킹포장WorldRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/마트피킹포장WorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.I물류이동AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/물류이동Models.cs` |
| `Ssalddel.Unity.Runtime.World.I실제E5AreaSetNetworkRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/실제E5AreaSetNetworkStreaming.cs` |
| `Ssalddel.Unity.Runtime.World.I오늘작업계획AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/오늘작업계획Models.cs` |
| `Ssalddel.Unity.Runtime.World.I정착지상호작용AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/정착지상호작용Models.cs` |
| `Ssalddel.Unity.Runtime.World.I진부Hub입고UiAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/진부Hub입고UiModels.cs` |
| `Ssalddel.Unity.Runtime.World.I타로턴마감AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/턴마감Models.cs` |
| `Ssalddel.Unity.Runtime.World.I턴마감AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/턴마감Models.cs` |
| `Ssalddel.Unity.Runtime.World.I협동창고AuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/협동창고Models.cs` |
| `Ssalddel.Unity.Runtime.World.Nature심상Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/Nature심상Models.cs` |
| `Ssalddel.Unity.Runtime.World.Npc업무행동ProjectionStore` | `E8` | `Annotated` | `Assets/Ssalddel/Runtime/World/Npc업무행동Models.cs` |
| `Ssalddel.Unity.Runtime.World.WorldBuildManifestValidator` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간TileAreaSetPipelineModels.cs` |
| `Ssalddel.Unity.Runtime.World.WorldCharacterAssignmentPolicy` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/역할CharacterAssignmentModels.cs` |
| `Ssalddel.Unity.Runtime.World.공간LandscapeGraphStreamingPlanner` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간LandscapeGraphStreaming.cs` |
| `Ssalddel.Unity.Runtime.World.공간TileWindowPlanner` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간TileStreamingModels.cs` |
| `Ssalddel.Unity.Runtime.World.대관령Farm공간LHWorldFixtureRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간LHWorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.대관령Farm공간TileStreamFixtureRepository` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/공간TileStreamingModels.cs` |
| `Ssalddel.Unity.Runtime.World.대관령L2창고아이템Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/대관령L2창고아이템Models.cs` |
| `Ssalddel.Unity.Runtime.World.대관령L2창고아이템FixtureAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/대관령L2창고아이템FixtureAuthorityClient.cs` |
| `Ssalddel.Unity.Runtime.World.마트피킹포장WorldStateStore` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/마트피킹포장WorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.물류이동Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/물류이동Models.cs` |
| `Ssalddel.Unity.Runtime.World.물류이동FixtureAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/물류이동Models.cs` |
| `Ssalddel.Unity.Runtime.World.법정동WorldProjectionValidator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/대한민국법정동WorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.법정동경관PlanValidator` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/대한민국법정동WorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.수확판로결과Projector` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/정착지상호작용Models.cs` |
| `Ssalddel.Unity.Runtime.World.오늘작업계획Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/오늘작업계획Models.cs` |
| `Ssalddel.Unity.Runtime.World.운영자재고ShelfProjector` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/마트피킹포장WorldModels.cs` |
| `Ssalddel.Unity.Runtime.World.정적경관배치PlanValidator` | `E5` | `Annotated` | `Assets/Ssalddel/Runtime/World/정적경관배치PlanModels.cs` |
| `Ssalddel.Unity.Runtime.World.정착지상호작용Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/정착지상호작용Models.cs` |
| `Ssalddel.Unity.Runtime.World.정착지상호작용FixtureAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/정착지상호작용Models.cs` |
| `Ssalddel.Unity.Runtime.World.진부Hub입고UiCoordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/진부Hub입고UiModels.cs` |
| `Ssalddel.Unity.Runtime.World.진부Hub입고UiFixtureAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/진부Hub입고UiFixtureAuthorityClient.cs` |
| `Ssalddel.Unity.Runtime.World.턴마감FixtureAuthorityClient` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/턴마감Models.cs` |
| `Ssalddel.Unity.Runtime.World.협동창고Coordinator` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/World/협동창고Models.cs` |
| `Ssalddel.Unity.Runtime.WorldMap.ICommunityWorldMapRepository` | `E2` | `Annotated` | `Assets/Ssalddel/Runtime/WorldMap/PublicWorldMapModels.cs` |
| `Ssalddel.Unity.Editor.Experiments.SyntyCityPackVerticalSliceBuilder` | `` | `Excluded` | `Assets/Ssalddel/Experiments - 연구/SyntyCityPackIntegration/Editor/SyntyCityPackVerticalSliceBuilder.cs` |
| `Ssalddel.Unity.Tests.EditMode.AreaSet구성패턴검토실Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/AreaSet구성패턴검토실Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.AreaSet접근Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/AreaSet접근Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.CityFarmSyntyWorldSceneTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/CityFarmSyntyWorldSceneTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.DioramaCameraTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/DioramaCameraTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Editor메뉴경계Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Editor메뉴경계Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Farm창고건설Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Farm창고건설Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Farm플레이어배치Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Farm플레이어배치Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.H2공간조합검토RootTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/H2공간조합검토RootTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.H5세계배치Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/H5세계배치Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.HarvestDispositionBranchAdapterTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/HarvestDispositionBranchAdapterTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Hosted비콘Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Hosted비콘Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.H계층실증ShellTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/H계층실증ShellTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.LocalSessionGameplayAuthorityRepositoryTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/LocalSessionGameplayAuthorityRepositoryTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.MarketSeedbedApiClientTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/MarketSeedbedApiClientTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Nature생존CoreRuntimeAdapterTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Nature생존CoreRuntimeAdapterTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Nature생존LocalEngineTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Nature생존LocalEngineTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Nature심상Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Nature심상Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Npc업무행동Tests` | `E8` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Npc업무행동Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.PublicWorldMapInteractionTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/PublicWorldMapInteractionTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.PublicWorldMapTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/PublicWorldMapTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.SimulationWorldShellTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/SimulationWorldShellTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.SyntyPackAssetInventoryTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/SyntyPackAssetInventoryTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.Synty공간조립Web검토CapturePipelineTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/Synty공간조립Web검토CapturePipelineTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.UnityEvidenceResponsibilityMetadataTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/UnityEvidenceResponsibilityMetadataTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.WI공간모판검토실Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/WI공간모판검토실Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.WorldBootstrapSceneTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/WorldBootstrapSceneTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.WorldBootstrapTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/WorldBootstrapTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.WorldVisualCatalogTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/WorldVisualCatalogTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공간AreaSetLandscapeGraphTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공간AreaSetLandscapeGraphTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공간LHStreamingTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공간LHStreamingTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공간TileAreaSetPipelineTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공간TileAreaSetPipelineTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공간TileStreamingTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공간TileStreamingTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공간문법LandscapeRuntimeTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공간문법LandscapeRuntimeTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공급망MacroWorldSceneTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공급망MacroWorldSceneTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.공급망WorldLayoutTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/공급망WorldLayoutTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.네이처H3GameViewSceneTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/네이처H3GameViewSceneTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.네이처생활핵조우방어공간Tests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/네이처생활핵조우방어공간Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.네이처탐험조우Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/네이처탐험조우Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.농장경영시점Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/농장경영시점Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.농장생존표현ProjectionTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/농장생존표현ProjectionTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.농장전투입력Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/농장전투입력Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.농장풍경CompositionSetTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/농장풍경CompositionSetTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.대관령L2창고아이템Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/대관령L2창고아이템Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.대한민국법정동WorldTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/대한민국법정동WorldTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.마트피킹포장WorldLoadingTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/마트피킹포장WorldLoadingTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.몰입경관InstanceTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/몰입경관InstanceTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.물류이동Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/물류이동Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.법정동Synty후단연결Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/법정동Synty후단연결Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.실제E5AreaSetNetworkTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/실제E5AreaSetNetworkTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.역할CharacterAssignmentTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/역할CharacterAssignmentTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.연구Scene한국어이름Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/연구Scene한국어이름Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.오늘작업계획Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/오늘작업계획Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.월드CompositionContractTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/월드CompositionContractTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.자연경관CompositionCatalogTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/자연경관CompositionCatalogTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.전술분대이동Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/전술분대이동Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.전장파생공간AssemblerTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/전장파생공간AssemblerTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.정적경관배치PlanPipelineTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/정적경관배치PlanPipelineTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.정착지상호작용AuthorityBoundaryTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/정착지상호작용AuthorityBoundaryTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.정착지상호작용Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/정착지상호작용Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.진부Hub입고UiTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/진부Hub입고UiTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.타로턴화면Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/타로턴화면Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.턴마감Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/턴마감Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.턴카드모판Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/턴카드모판Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합SimulationWorldTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합SimulationWorldTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합WorldScenePolicyTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합WorldScenePolicyTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합전시관Object모판Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합전시관Object모판Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합전시관ScenePlacementTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합전시관ScenePlacementTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합전시관SimulationLoadingTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합전시관SimulationLoadingTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.통합전시관Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/통합전시관Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.팀원관전CameraTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/팀원관전CameraTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.팀원관전PipelineTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/팀원관전PipelineTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.팩경관CompositionCatalogTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/팩경관CompositionCatalogTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.평창4Pack경관ProfileTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/평창4Pack경관ProfileTests.cs` |
| `Ssalddel.Unity.Tests.EditMode.현장전투입력번역Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/현장전투입력번역Tests.cs` |
| `Ssalddel.Unity.Tests.EditMode.협동창고Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/EditMode/협동창고Tests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.H계층실증PlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/H계층실증PlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.PublicWorldMapPresenterPlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/PublicWorldMapPresenterPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.공간TileLodLoaderPlayModeTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/공간TileLodLoaderPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.공간TileStreamingPlayModeTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/공간TileStreamingPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.네이처H3하천이동Tests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/네이처H3하천이동Tests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.네이처탐험조우PlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/네이처탐험조우PlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.역할CharacterVisualPlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/역할CharacterVisualPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.전략카메라PlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/PublicWorldMapPresenterPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.전술분대이동PlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/전술분대이동PlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.진부Hub입고UiPlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/진부Hub입고UiPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.통합SimulationWorldPlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/통합SimulationWorldPlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.통합월드자유이동PlayModeTests` | `E3` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/통합월드자유이동PlayModeTests.cs` |
| `Ssalddel.Unity.Tests.PlayMode.플레이어경관PlayModeTests` | `E5` | `Annotated` | `Assets/Ssalddel/Tests/PlayMode/PublicWorldMapPresenterPlayModeTests.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.Editor.도심물류센터PrimitiveSceneBuilder` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Editor/도심물류센터PrimitiveSceneBuilder.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.Tests.Editor.LogisticsFacilityOverviewViewTests` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Tests/Editor/LogisticsFacilityOverviewViewTests.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.LogisticsFacilityOverviewView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/LogisticsFacilityOverviewView.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.LogisticsInteractionPanelView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/LogisticsInteractionPanelView.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.LogisticsRoleTargetView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/LogisticsRoleTargetView.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.OperationalCargoWarehouseHandoffApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/OperationalWorldApiClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.OperationalUrbanLogisticsNpcApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/OperationalWorldApiClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.OperationalUrbanLogisticsRoleApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/OperationalWorldApiClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.SimulatedCargoWarehouseHandoffApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/SimulatedUrbanLogisticsClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.SimulatedUrbanLogisticsNpcApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/SimulatedUrbanLogisticsClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.SimulatedUrbanLogisticsRoleApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/SimulatedUrbanLogisticsClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.TransportCorridorTruckView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/TransportCorridorTruckView.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.UnityWebRequestWorldGetClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/OperationalWorldApiClients.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.도심물류센터SceneController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/도심물류센터SceneController.cs` |
| `Ssalddel.Unity.Samples.UrbanLogisticsCenter.도심물류센터View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Logistics Center Primitive Vertical Slice/Runtime/도심물류센터View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.Editor.도심마트ManagerPrimitiveSceneBuilder` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Editor/도심마트ManagerPrimitiveSceneBuilder.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.Editor.도심마트PrimitiveSceneBuilder` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Editor/도심마트PrimitiveSceneBuilder.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.Tests.Editor.UrbanMarketConceptCardAssetReadinessTests` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Tests/Editor/UrbanMarketConceptCardAssetReadinessTests.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.ConceptCardDeckView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/ConceptCardDeckView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.ConceptCardView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/ConceptCardView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.OperationalUrbanMarketApiClient` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/OperationalUrbanMarketApiClient.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.가격표View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/가격표View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.공동주택대표NpcView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/공동주택대표NpcView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트ConceptCardSceneController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트ConceptCardSceneController.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트ManagerSceneController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트ManagerSceneController.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트ManagerShelfView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트ManagerShelfView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트ManagerSurfaceView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트ManagerSurfaceView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트SceneController` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트SceneController.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.도심마트공급경영SurfaceView` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/도심마트공급경영SurfaceView.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.상품상자View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/상품상자View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.상품진열대View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/상품진열대View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.재고상태View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/재고상태View.cs` |
| `Ssalddel.Unity.Samples.UrbanMarket.정보키오스크View` | `` | `Excluded` | `Assets/Samples/Ssalddel Simulation Data/0.1.0/Urban Market Primitive Vertical Slice/Runtime/정보키오스크View.cs` |
