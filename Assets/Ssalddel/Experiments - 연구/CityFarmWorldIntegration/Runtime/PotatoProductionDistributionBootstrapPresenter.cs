using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.PotatoJourney;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    /// <summary>
    /// 게임 시작 시 이미 구성된 authorized API client로 감자생산유통 World를 읽고,
    /// stable-ID 메모리와 Visual Catalog reference를 준비합니다. Prefab을 업무 권위로 사용하지 않습니다.
    /// </summary>
    public sealed class PotatoProductionDistributionBootstrapPresenter : MonoBehaviour
    {
        [SerializeField] private WorldVisualCatalog farmCatalog = null!;
        [SerializeField] private WorldVisualCatalog urbanCatalog = null!;
        [SerializeField] private bool loadOnStart = true;

        private readonly PotatoProductionDistributionWorldMemoryStore memoryStore = new();
        private readonly Dictionary<string, WorldVisualCatalogEntry> warmedVisuals =
            new(StringComparer.Ordinal);
        private IPotatoJourneyApiClient? apiClient;
        private string cultivationStableId = string.Empty;
        private string cacheBoundaryKey = string.Empty;

        public string StateCode { get; private set; } = "Idle";
        public string ErrorCode { get; private set; } = string.Empty;
        public PotatoProductionDistributionWorldMemoryStore MemoryStore => memoryStore;
        public IReadOnlyDictionary<string, WorldVisualCatalogEntry> WarmedVisuals => warmedVisuals;

        public void Configure(
            IPotatoJourneyApiClient configuredClient,
            string configuredCacheBoundaryKey,
            string? configuredCultivationStableId = null)
        {
            apiClient = configuredClient ?? throw new ArgumentNullException(nameof(configuredClient));
            if (string.IsNullOrWhiteSpace(configuredCacheBoundaryKey))
                throw new ArgumentException("PotatoBootstrapCacheBoundaryMissing", nameof(configuredCacheBoundaryKey));
            cacheBoundaryKey = configuredCacheBoundaryKey.Trim();
            cultivationStableId = configuredCultivationStableId?.Trim() ?? string.Empty;
        }

        public void ConfigureCatalogs(WorldVisualCatalog configuredFarm, WorldVisualCatalog configuredUrban)
        {
            farmCatalog = configuredFarm ?? throw new ArgumentNullException(nameof(configuredFarm));
            urbanCatalog = configuredUrban ?? throw new ArgumentNullException(nameof(configuredUrban));
        }

        private void Start()
        {
            if (loadOnStart && apiClient != null)
                _ = LoadAndWarmAsync(destroyCancellationToken);
        }

        public async Task<PotatoProductionDistributionBootstrapResult> LoadAndWarmAsync(
            CancellationToken cancellationToken = default)
        {
            if (apiClient == null) throw new InvalidOperationException("PotatoBootstrapApiClientNotConfigured");
            ValidateCatalogs();
            StateCode = "Loading";
            ErrorCode = string.Empty;
            try
            {
                var loader = new PotatoProductionDistributionWorldBootstrapLoader(
                    new PotatoJourneyQueryUseCase(
                        new PotatoJourneyApiRepository(apiClient, new PotatoJourneyMapper())),
                    memoryStore);
                var result = await loader.LoadAsync(
                    cultivationStableId.Length == 0 ? null : cultivationStableId,
                    cacheBoundaryKey,
                    cancellationToken);
                WarmVisuals();
                StateCode = "Ready";
                return result;
            }
            catch (OperationCanceledException)
            {
                StateCode = memoryStore.Current == null ? "Idle" : "Ready";
                throw;
            }
            catch (Exception error)
            {
                StateCode = "Error";
                ErrorCode = Normalize(error.Message);
                throw;
            }
        }

        public void WarmVisuals()
        {
            ValidateCatalogs();
            var resolved = new Dictionary<string, WorldVisualCatalogEntry>(StringComparer.Ordinal);
            foreach (var node in memoryStore.Nodes)
            {
                var catalog = node.VisualKey.StartsWith("farm.", StringComparison.Ordinal)
                    ? farmCatalog
                    : node.VisualKey.StartsWith("urban.", StringComparison.Ordinal)
                        ? urbanCatalog
                        : throw new InvalidOperationException("PotatoBootstrapVisualCatalogUnknown:" + node.VisualKey);
                resolved.Add(node.StableId, catalog.Resolve(node.VisualKey));
            }

            warmedVisuals.Clear();
            foreach (var pair in resolved) warmedVisuals.Add(pair.Key, pair.Value);
        }

        public bool ValidateWiring()
            => farmCatalog != null && urbanCatalog != null;

        private void ValidateCatalogs()
        {
            if (!ValidateWiring()) throw new InvalidOperationException("PotatoBootstrapCatalogMissing");
            farmCatalog.Validate();
            urbanCatalog.Validate();
        }

        private static string Normalize(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "PotatoBootstrapFailed";
            var separator = message.IndexOf(':');
            return separator < 0 ? message : message.Substring(0, separator);
        }
    }
}
