using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public interface IStationDioramaModuleAdapter
    {
        string TransitStationStableId { get; }
        bool ModuleVisible { get; }
        IReadOnlyList<StationDioramaModuleLayerRuntimeState> DescribeModuleLayers();
        void SetModuleVisible(bool value);
    }

    /// <summary>
    /// canonical SimulationWorldShell 안에서 역세권 하나의 읽기 전용 Adapter만 활성화한다.
    /// 자료를 읽거나 Mesh를 생성하지 않으며 등록되지 않은 역을 다른 역 자료로 대체하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveStationDioramaHost : MonoBehaviour
    {
        [SerializeField]
        private string requestedTransitStationStableId =
            StationDioramaProfileCatalog.SagajeongStationStableId;

        private readonly List<IStationDioramaModuleAdapter> adapters = new();

        public string RequestedTransitStationStableId => requestedTransitStationStableId;
        public string ActiveTransitStationStableId { get; private set; } = string.Empty;
        public StationDioramaModuleActivationPlan ActivePlan { get; private set; }
        public int RegisteredAdapterCount => adapters.Count;
        public string StatusCode { get; private set; } = "WaitingForModuleAdapter";

        public void Register(IStationDioramaModuleAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (!StationDioramaModuleProfileCatalog.TryResolve(adapter.TransitStationStableId,
                    out var profile))
                throw new InvalidOperationException("StationDioramaModuleProfileNotRegistered");
            if (adapters.Contains(adapter))
            {
                if (string.Equals(ActiveTransitStationStableId,
                        adapter.TransitStationStableId, StringComparison.Ordinal))
                    Refresh();
                return;
            }

            // 등록이 거절되더라도 Host 밖의 표현이 우연히 노출되지 않게 한다.
            // 정상 등록이면 같은 호출 안의 TryActivate/Refresh가 가용 모듈만 복원한다.
            adapter.SetModuleVisible(false);
            StationDioramaModulePlanner.Build(profile,
                Contributions(adapter.TransitStationStableId)
                    .Concat(adapter.DescribeModuleLayers()
                            ?? Array.Empty<StationDioramaModuleLayerRuntimeState>()));
            adapters.Add(adapter);
            if (string.IsNullOrEmpty(ActiveTransitStationStableId))
                TryActivate(requestedTransitStationStableId);
            else if (string.Equals(ActiveTransitStationStableId,
                         adapter.TransitStationStableId, StringComparison.Ordinal))
                Refresh();
            else
                adapter.SetModuleVisible(false);
        }

        public void Unregister(IStationDioramaModuleAdapter adapter)
        {
            if (adapter == null || !adapters.Remove(adapter)) return;
            adapter.SetModuleVisible(false);
            if (string.Equals(ActiveTransitStationStableId, adapter.TransitStationStableId,
                    StringComparison.Ordinal))
                Refresh();
        }

        public bool TryActivate(string transitStationStableId)
        {
            if (!StationDioramaModuleProfileCatalog.TryResolve(transitStationStableId,
                    out var profile))
            {
                StatusCode = "StationDioramaModuleProfileNotRegistered";
                return false;
            }

            StationDioramaModuleActivationPlan candidate;
            try
            {
                candidate = StationDioramaModulePlanner.Build(profile,
                    Contributions(transitStationStableId));
            }
            catch (ArgumentException)
            {
                StatusCode = "StationDioramaModuleContributionRejected";
                return false;
            }
            if (!candidate.CanPresentObservation)
            {
                if (string.IsNullOrEmpty(ActiveTransitStationStableId)
                    || string.Equals(ActiveTransitStationStableId, transitStationStableId,
                        StringComparison.Ordinal))
                {
                    ActivePlan = candidate;
                    ApplyVisibility(string.Empty, candidate);
                    ActiveTransitStationStableId = string.Empty;
                }
                StatusCode = "WaitingForRequiredModule";
                return false;
            }

            requestedTransitStationStableId = transitStationStableId;
            ActiveTransitStationStableId = transitStationStableId;
            ActivePlan = candidate;
            ApplyVisibility(transitStationStableId, candidate);
            StatusCode = candidate.OptionalLayersComplete
                ? "Ready"
                : "ReadyWithOptionalModuleGaps";
            return true;
        }

        public bool Refresh()
        {
            var target = string.IsNullOrEmpty(ActiveTransitStationStableId)
                ? requestedTransitStationStableId
                : ActiveTransitStationStableId;
            return TryActivate(target);
        }

        private IEnumerable<StationDioramaModuleLayerRuntimeState> Contributions(
            string transitStationStableId)
            => adapters.Where(value => string.Equals(value.TransitStationStableId,
                    transitStationStableId, StringComparison.Ordinal))
                .SelectMany(value => value.DescribeModuleLayers()
                                      ?? Array.Empty<StationDioramaModuleLayerRuntimeState>());

        private void ApplyVisibility(string transitStationStableId,
            StationDioramaModuleActivationPlan plan = null)
        {
            var availableLayers = new HashSet<string>(
                plan?.Layers.Where(value => value.AvailableForObservation)
                    .Select(value => value.Definition.LayerStableId)
                ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var adapter in adapters)
            {
                var stationMatches = !string.IsNullOrEmpty(transitStationStableId)
                                     && string.Equals(adapter.TransitStationStableId,
                                         transitStationStableId, StringComparison.Ordinal);
                var hasAvailableLayer = stationMatches
                                        && (adapter.DescribeModuleLayers()
                                            ?? Array.Empty<StationDioramaModuleLayerRuntimeState>())
                                        .Any(value => availableLayers.Contains(
                                            value.LayerStableId));
                adapter.SetModuleVisible(hasAvailableLayer);
            }
        }

        private void OnDestroy()
        {
            ApplyVisibility(string.Empty);
            adapters.Clear();
            ActivePlan = null;
            ActiveTransitStationStableId = string.Empty;
        }
    }
}
