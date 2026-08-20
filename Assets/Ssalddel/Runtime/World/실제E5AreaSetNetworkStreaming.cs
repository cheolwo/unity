using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public interface I실제E5AreaSetNetworkRepository
    {
        Task<실제E5AreaSetNetworkData> LoadAreaSetNetworkAsync(
            string networkStableId, CancellationToken cancellationToken);
        Task<실제E5InteractionReadinessData> LoadInteractionReadinessAsync(
            string networkStableId, CancellationToken cancellationToken);
    }

    public sealed class 실제E5AreaSetNetworkStreamingBatch
    {
        public 실제E5AreaSetNetworkData Network = null!;
        public 실제E5InteractionReadinessData InteractionReadiness = null!;
        public string PreviousAreaSetStableId = string.Empty;
        public string ActiveAreaSetStableId = string.Empty;
        public 공간LandscapeGraphStreamingBatch[] AreaBatches =
            Array.Empty<공간LandscapeGraphStreamingBatch>();
        public 공간LandscapeGraphData[] LoadedRouteGraphs =
            Array.Empty<공간LandscapeGraphData>();
    }

    /// <summary>
    /// Nature는 상시 유지하고 Farm·Hub·Town은 선택한 지역만 활성화한다.
    /// 서버 Graph payload와 WI 준비도를 읽기만 하며 WorldTick이나 업무 상태를 만들지 않는다.
    /// </summary>
    public sealed class 실제E5AreaSetNetworkStreamingSession
    {
        private readonly I실제E5AreaSetNetworkRepository networkRepository;
        private readonly I공간AreaSetLandscapeGraphRepository graphRepository;
        private readonly Dictionary<string, 공간LandscapeGraphStreamingSession> areaSessions =
            new Dictionary<string, 공간LandscapeGraphStreamingSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, 공간LandscapeGraphData> routeGraphs =
            new Dictionary<string, 공간LandscapeGraphData>(StringComparer.Ordinal);

        public 실제E5AreaSetNetworkStreamingSession(
            I실제E5AreaSetNetworkRepository sourceNetworkRepository,
            I공간AreaSetLandscapeGraphRepository sourceGraphRepository)
        {
            networkRepository = sourceNetworkRepository
                                ?? throw new ArgumentNullException(nameof(sourceNetworkRepository));
            graphRepository = sourceGraphRepository
                              ?? throw new ArgumentNullException(nameof(sourceGraphRepository));
        }

        public 실제E5AreaSetNetworkData Network { get; private set; }
        public 실제E5InteractionReadinessData InteractionReadiness { get; private set; }
        public string ActiveAreaSetStableId { get; private set; } = string.Empty;

        public async Task<실제E5AreaSetNetworkStreamingBatch> InitializeAsync(
            CancellationToken cancellationToken)
        {
            Network = await networkRepository.LoadAreaSetNetworkAsync(
                공간AreaSetLandscapeGraphCodes.ActualE5Network, cancellationToken);
            Network.Validate();
            InteractionReadiness = await networkRepository.LoadInteractionReadinessAsync(
                Network.NetworkStableId, cancellationToken);
            InteractionReadiness.Validate();
            foreach (var area in Network.AreaSets.OrderBy(
                         value => value.AreaSetStableId, StringComparer.Ordinal))
            {
                var session = new 공간LandscapeGraphStreamingSession(graphRepository);
                var definition = await session.InitializeAsync(
                    area.AreaSetStableId, cancellationToken);
                if (definition.CanonicalNetworkStableId != Network.NetworkStableId
                    || definition.Revision != area.AreaSetRevision
                    || definition.DefinitionHashSha256 != area.DefinitionHashSha256)
                    throw new InvalidOperationException("ActualE5AreaSetDefinitionMismatch");
                areaSessions.Add(area.AreaSetStableId, session);
            }
            return await ActivateAreaAsync(
                실제E5AreaSetNetworkCodes.NatureAreaSet, cancellationToken);
        }

        public async Task<실제E5AreaSetNetworkStreamingBatch> ActivateAreaAsync(
            string areaSetStableId,
            CancellationToken cancellationToken)
        {
            if (Network == null || InteractionReadiness == null)
                throw new InvalidOperationException("ActualE5AreaSetNetworkNotInitialized");
            if (!areaSessions.ContainsKey(areaSetStableId))
                throw new InvalidOperationException("ActualE5AreaSetUnknown");
            var previous = ActiveAreaSetStableId;
            var areaBatches = new List<공간LandscapeGraphStreamingBatch>();
            foreach (var area in Network.AreaSets.OrderBy(
                         value => value.AreaSetStableId, StringComparer.Ordinal))
            {
                var session = areaSessions[area.AreaSetStableId];
                var tileKeys = session.AreaSet.LandscapeGraphs
                    .SelectMany(value => value.TileRefs)
                    .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
                if (tileKeys.Count == 0)
                    throw new InvalidOperationException("ActualE5AreaSetTileRefsMissing");
                var active = area.LoadPolicyCode == 실제E5AreaSetNetworkCodes.Persistent
                             || area.AreaSetStableId == areaSetStableId
                    ? tileKeys
                    : new HashSet<string>(StringComparer.Ordinal);
                areaBatches.Add(await session.RefreshAsync(
                    tileKeys.OrderBy(value => value, StringComparer.Ordinal).First(),
                    active,
                    new HashSet<string>(StringComparer.Ordinal),
                    0,
                    cancellationToken));
            }

            var loadedRoutes = new List<공간LandscapeGraphData>();
            if (!string.IsNullOrWhiteSpace(previous) && previous != areaSetStableId)
            {
                foreach (var relation in FindTransitionPath(previous, areaSetStableId))
                {
                    if (string.IsNullOrWhiteSpace(relation.RouteGraphStableId)
                        || routeGraphs.ContainsKey(relation.RouteGraphStableId))
                        continue;
                    var route = await graphRepository.LoadGraphAsync(
                        relation.RouteGraphStableId, cancellationToken);
                    route.Validate();
                    if (route.SpatialOwnerKindCode !=
                            공간AreaSetLandscapeGraphCodes.AreaSetNetworkOwner
                        || route.SpatialOwnerStableId != Network.NetworkStableId)
                        throw new InvalidOperationException("ActualE5RouteGraphPayloadMismatch");
                    routeGraphs.Add(route.LandscapeGraphStableId, route);
                    loadedRoutes.Add(route);
                }
            }
            ActiveAreaSetStableId = areaSetStableId;
            return new 실제E5AreaSetNetworkStreamingBatch
            {
                Network = Network,
                InteractionReadiness = InteractionReadiness,
                PreviousAreaSetStableId = previous,
                ActiveAreaSetStableId = ActiveAreaSetStableId,
                AreaBatches = areaBatches.ToArray(),
                LoadedRouteGraphs = loadedRoutes.ToArray(),
            };
        }

        private 실제E5NetworkRelationData[] FindTransitionPath(
            string fromAreaSetStableId,
            string toAreaSetStableId)
        {
            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal)
            {
                fromAreaSetStableId,
            };
            var previousByArea = new Dictionary<string, 실제E5NetworkRelationData>(
                StringComparer.Ordinal);
            queue.Enqueue(fromAreaSetStableId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var relation in Network.Relations
                             .Where(value => value.FromAreaSetStableId == current)
                             .OrderBy(value => value.RelationStableId, StringComparer.Ordinal))
                {
                    if (!visited.Add(relation.ToAreaSetStableId)) continue;
                    previousByArea.Add(relation.ToAreaSetStableId, relation);
                    if (relation.ToAreaSetStableId == toAreaSetStableId)
                    {
                        var path = new List<실제E5NetworkRelationData>();
                        var cursor = toAreaSetStableId;
                        while (cursor != fromAreaSetStableId)
                        {
                            var step = previousByArea[cursor];
                            path.Add(step);
                            cursor = step.FromAreaSetStableId;
                        }
                        path.Reverse();
                        return path.ToArray();
                    }
                    queue.Enqueue(relation.ToAreaSetStableId);
                }
            }
            throw new InvalidOperationException("ActualE5AreaSetTransitionUnavailable");
        }
    }
}
