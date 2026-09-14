using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 기존 사가정 교통·배달 Playback을 다시 구현하지 않고 공통 Mobility 모듈에 연결한다.
    /// 자료 판정과 이동 계산은 각 Playback 앞의 기존 Interpreter가 계속 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 사가정MobilityModuleAdapter : StationDioramaSingleLayerAdapter
    {
        public const string AdapterRevision =
            "station-diorama-module:sagajeong-mobility-adapter.r1";
        public const string LowDensityTrafficSourceStableId =
            "presentation-source:sagajeong-low-density-traffic";
        public const string FoodDeliveryJourneySourceStableId =
            "presentation-source:food-delivery-journey";

        private readonly List<IStationDioramaModulePresentationSource> sources = new();
        private readonly Dictionary<Behaviour, bool> enabledBeforeHide = new();

        public override string TransitStationStableId =>
            StationDioramaProfileCatalog.SagajeongStationStableId;
        public override string LayerStableId => StationDioramaModuleLayerIds.Mobility;
        public int SourceCount => sources.Count;
        public int ReadySourceCount => sources.Count(value => value.HasAcceptedPresentation);

        /// <summary>
        /// 기존 Playback에 새 상태 사본을 적용한 직후 Host의 준비도와 표시 여부를 다시 계산한다.
        /// Playback의 Apply 수명은 바꾸지 않으며 실제 조립부가 적용 완료 뒤 명시적으로 호출한다.
        /// </summary>
        public bool RefreshModuleState()
            => RefreshHost();

        public void Configure(ActiveStationDioramaHost host,
            사가정저밀도교통PlaybackLayer lowDensityTraffic)
            => ConfigureSources(host, lowDensityTraffic == null
                ? Array.Empty<IStationDioramaModulePresentationSource>()
                : new IStationDioramaModulePresentationSource[]
                    { new LowDensityTrafficSource(lowDensityTraffic) });

        public void Configure(ActiveStationDioramaHost host,
            음식배달JourneyPlaybackLayer foodDeliveryJourney)
            => ConfigureSources(host, foodDeliveryJourney == null
                ? Array.Empty<IStationDioramaModulePresentationSource>()
                : new IStationDioramaModulePresentationSource[]
                    { new FoodDeliveryJourneySource(foodDeliveryJourney) });

        public void ConfigureSources(ActiveStationDioramaHost host,
            IEnumerable<IStationDioramaModulePresentationSource> values)
        {
            var incoming = (values ?? Array.Empty<IStationDioramaModulePresentationSource>())
                .ToArray();
            if (incoming.Any(value => value == null))
                throw new ArgumentException("StationDioramaModuleSourceNull", nameof(values));
            if (incoming.Length > 1)
                throw new ArgumentException("StationDioramaModuleSourceAmbiguous",
                    nameof(values));
            if (incoming.Any(value => !string.Equals(value.ModuleLayerStableId,
                    LayerStableId, StringComparison.Ordinal)))
                throw new ArgumentException("StationDioramaModuleSourceLayerMismatch",
                    nameof(values));
            if (incoming.Any(value => string.IsNullOrWhiteSpace(value.ModuleSourceStableId))
                || incoming.GroupBy(value => value.ModuleSourceStableId,
                        StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw new ArgumentException("StationDioramaModuleSourceStableIdInvalid",
                    nameof(values));

            if (!ModuleVisible) RestorePresentationTargets();
            sources.Clear();
            sources.AddRange(incoming.OrderBy(value => value.ModuleSourceStableId,
                StringComparer.Ordinal));
            if (!ModuleVisible) HidePresentationTargets();
            AttachToHost(host);
        }

        protected override StationDioramaModuleLayerRuntimeState DescribeLayerState()
        {
            if (sources.Count == 0)
                return StationDioramaModuleLayerRuntimeStateFactory.NotProvided(
                    LayerStableId, "WaitingForMobilityPlayback");

            var source = sources[0];
            if (!source.HasAcceptedPresentation)
                return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    LayerStableId, false, AdapterRevision,
                    string.IsNullOrWhiteSpace(source.ModuleDiagnosticCode)
                        ? "WaitingForMobilitySnapshot"
                        : source.ModuleDiagnosticCode);
            if (string.IsNullOrWhiteSpace(source.ModuleSourceRevision))
                return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    LayerStableId, false, AdapterRevision,
                    "StationDioramaModuleSourceRevisionMissing");

            var revision = AdapterRevision + "|" + source.ModuleSourceStableId
                           + "@" + source.ModuleSourceRevision;
            var diagnostic = string.IsNullOrWhiteSpace(source.ModuleDiagnosticCode)
                ? Diagnostic(source.ModuleSourceStableId)
                : source.ModuleDiagnosticCode;
            return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                LayerStableId, true, revision, diagnostic);
        }

        protected override void OnModuleVisibilityChanged(bool value)
        {
            if (value) RestorePresentationTargets();
            else HidePresentationTargets();
        }

        private void HidePresentationTargets()
        {
            if (enabledBeforeHide.Count > 0) return;
            foreach (var target in PresentationTargets())
            {
                enabledBeforeHide.Add(target, target.enabled);
                target.enabled = false;
            }
        }

        private void RestorePresentationTargets()
        {
            foreach (var pair in enabledBeforeHide.ToArray())
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            enabledBeforeHide.Clear();
        }

        private IEnumerable<Behaviour> PresentationTargets()
            => sources.Select(value => value.PresentationBehaviour)
                .Where(value => value != null)
                .Distinct();

        private static string Diagnostic(string sourceStableId)
        {
            if (string.Equals(sourceStableId, LowDensityTrafficSourceStableId,
                    StringComparison.Ordinal))
                return "SagajeongLowDensityTrafficReady";
            if (string.Equals(sourceStableId, FoodDeliveryJourneySourceStableId,
                    StringComparison.Ordinal))
                return "SagajeongFoodDeliveryJourneyReady";
            return "SagajeongMobilityReady";
        }

        private sealed class LowDensityTrafficSource :
            IStationDioramaModulePresentationSource
        {
            private readonly 사가정저밀도교통PlaybackLayer playback;

            public LowDensityTrafficSource(사가정저밀도교통PlaybackLayer value)
                => playback = value;

            public string ModuleLayerStableId => StationDioramaModuleLayerIds.Mobility;
            public string ModuleSourceStableId => LowDensityTrafficSourceStableId;
            public string ModuleSourceRevision =>
                사가정저밀도교통Fixture.SourceMobilityGraphRevision + "|"
                + 사가정저밀도교통Fixture.LaneGraphRevision + "|"
                + 사가정저밀도교통Fixture.SignalPlanRevision + "|"
                + 사가정저밀도교통Fixture.TrafficProfileRevision + "|"
                + 사가정저밀도교통Fixture.CourierDisplayRouteRevision;
            public string ModuleDiagnosticCode => HasAcceptedPresentation
                ? "SagajeongLowDensityTrafficReady"
                : "WaitingForSagajeongLowDensityTrafficSnapshot";
            public bool HasAcceptedPresentation =>
                playback.StaticGeometryReady && playback.AppliedRevision >= 0;
            public Behaviour PresentationBehaviour => playback;
        }

        private sealed class FoodDeliveryJourneySource :
            IStationDioramaModulePresentationSource
        {
            private readonly 음식배달JourneyPlaybackLayer playback;

            public FoodDeliveryJourneySource(음식배달JourneyPlaybackLayer value)
                => playback = value;

            public string ModuleLayerStableId => StationDioramaModuleLayerIds.Mobility;
            public string ModuleSourceStableId => FoodDeliveryJourneySourceStableId;
            public string ModuleSourceRevision => "food-delivery-journey-playback.v1";
            public string ModuleDiagnosticCode => HasAcceptedPresentation
                ? "SagajeongFoodDeliveryJourneyReady"
                : "WaitingForFoodDeliveryJourneySnapshot";
            public bool HasAcceptedPresentation => playback.ActiveVisualCount > 0;
            public Behaviour PresentationBehaviour => playback;
        }
    }
}
