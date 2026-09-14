using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 사가정 합성 기사 관찰층을 공통 역세권 LifeSimulation 모듈에 연결한다.
    /// 준비도와 Host 표시만 중계하며 Simulation 상태나 사용자 로컬 토글은 바꾸지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 사가정LifeSimulationModuleAdapter : StationDioramaSingleLayerAdapter
    {
        public const string AdapterRevision =
            "station-diorama-module:sagajeong-life-simulation-adapter.r1";

        private 사가정합성Actor관찰Layer observationLayer;

        public override string TransitStationStableId =>
            StationDioramaProfileCatalog.SagajeongStationStableId;

        public override string LayerStableId =>
            StationDioramaModuleLayerIds.LifeSimulation;

        protected override bool RestoreStandaloneVisibilityAfterDetach => false;

        public 사가정합성Actor관찰Layer ObservationLayer => observationLayer;

        public void Configure(ActiveStationDioramaHost host,
            사가정합성Actor관찰Layer layer)
        {
            observationLayer = layer != null
                ? layer
                : throw new ArgumentNullException(nameof(layer));
            observationLayer.SetHostVisible(ModuleVisible);
            AttachToHost(host ?? throw new ArgumentNullException(nameof(host)));
        }

        /// <summary>
        /// 새 관찰 사본 적용 뒤 공통 Host의 준비도와 조립 hash를 다시 계산한다.
        /// </summary>
        public bool RefreshModuleState() => RefreshHost();

        protected override StationDioramaModuleLayerRuntimeState DescribeLayerState()
        {
            if (observationLayer == null)
                return StationDioramaModuleLayerRuntimeStateFactory.NotProvided(
                    LayerStableId, "WaitingForSyntheticActorObservationLayer");

            if (!observationLayer.IsConfigured)
                return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    LayerStableId, false, AdapterRevision,
                    사가정합성Actor관찰Layer.WaitingForCoordinateBindingStatus);

            if (!observationLayer.HasAcceptedObservation
                || string.IsNullOrWhiteSpace(observationLayer.AppliedFingerprint))
                return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    LayerStableId, false, AdapterRevision,
                    string.IsNullOrWhiteSpace(observationLayer.StatusCode)
                        ? "WaitingForSyntheticActorObservationSnapshot"
                        : observationLayer.StatusCode);

            return StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                LayerStableId, true,
                AdapterRevision + "|" + observationLayer.AppliedFingerprint,
                사가정합성Actor관찰Layer.ReadyStatus);
        }

        protected override void OnModuleVisibilityChanged(bool value)
            => observationLayer?.SetHostVisible(value);

        // Unity 수명 메시지는 상속된 virtual 호출보다 구체 MonoBehaviour에
        // 선언된 메시지를 우선해 탐색하므로 이 Adapter가 직접 정리한다.
        private new void OnDestroy()
        {
            observationLayer?.SetHostVisible(false);
            observationLayer = null;
            base.OnDestroy();
        }

    }
}
