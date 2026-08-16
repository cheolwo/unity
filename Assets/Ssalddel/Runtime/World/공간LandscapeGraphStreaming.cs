using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public interface I공간AreaSetLandscapeGraphRepository
    {
        Task<공간AreaSetDefinitionData> LoadAreaSetAsync(
            string areaSetStableId, CancellationToken cancellationToken);
        Task<공간LandscapeGraphIndexData> LoadGraphIndexAsync(
            string areaSetStableId, string centerTileKey, int radiusTiles,
            CancellationToken cancellationToken);
        Task<공간LandscapeGraphData> LoadGraphAsync(
            string landscapeGraphStableId, CancellationToken cancellationToken);
    }

    public sealed class 공간LandscapeGraphStreamingDecision
    {
        public string LandscapeGraphStableId = string.Empty;
        public 공간LandscapeGraphStreamingState PreviousState;
        public 공간LandscapeGraphStreamingState NextState;
        public bool NeedsPayloadLoad;
        public bool NeedsActivation;
        public bool ReleasePayload;
    }

    public sealed class 공간LandscapeGraphStreamingBatch
    {
        public 공간AreaSetDefinitionData AreaSet = null!;
        public 공간LandscapeGraphIndexData Index = null!;
        public 공간LandscapeGraphStreamingDecision[] Decisions =
            Array.Empty<공간LandscapeGraphStreamingDecision>();
        public 공간LandscapeGraphData[] LoadedGraphs = Array.Empty<공간LandscapeGraphData>();
        public string[] ReleasedGraphStableIds = Array.Empty<string>();
    }

    public sealed class 공간LandscapeGraphStreamingPlanner
    {
        private readonly Dictionary<string, 공간LandscapeGraphStreamingState> states =
            new Dictionary<string, 공간LandscapeGraphStreamingState>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> hashes =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, 공간LandscapeGraphStreamingState> States => states;

        public IReadOnlyList<공간LandscapeGraphStreamingDecision> Reconcile(
            공간LandscapeGraphIndexData index,
            ISet<string> activeTileKeys,
            ISet<string> preparedTileKeys,
            int maximumCachedGraphs = 8)
        {
            index.Validate();
            if (activeTileKeys == null || preparedTileKeys == null || maximumCachedGraphs < 0)
                throw new InvalidOperationException("WorldLandscapeGraphStreamingInputInvalid");
            var decisions = new List<공간LandscapeGraphStreamingDecision>();
            var present = index.Graphs.Select(value => value.LandscapeGraphStableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var descriptor in index.Graphs.OrderBy(
                         value => value.LandscapeGraphStableId, StringComparer.Ordinal))
            {
                states.TryGetValue(descriptor.LandscapeGraphStableId, out var previous);
                var next = DetermineNextState(
                    descriptor, previous, activeTileKeys, preparedTileKeys);
                hashes.TryGetValue(descriptor.LandscapeGraphStableId, out var previousHash);
                decisions.Add(new 공간LandscapeGraphStreamingDecision
                {
                    LandscapeGraphStableId = descriptor.LandscapeGraphStableId,
                    PreviousState = previous,
                    NextState = next,
                    NeedsPayloadLoad = NeedsPayloadLoad(
                        descriptor, previous, next, previousHash),
                    NeedsActivation = next == 공간LandscapeGraphStreamingState.Active
                                      && previous != 공간LandscapeGraphStreamingState.Active,
                });
                states[descriptor.LandscapeGraphStableId] = next;
                hashes[descriptor.LandscapeGraphStableId] = descriptor.GraphHashSha256;
            }

            foreach (var missing in states.Keys.Where(value => !present.Contains(value))
                         .OrderBy(value => value, StringComparer.Ordinal).ToArray())
            {
                var previous = states[missing];
                var next = MissingGraphNextState(previous);
                decisions.Add(new 공간LandscapeGraphStreamingDecision
                {
                    LandscapeGraphStableId = missing,
                    PreviousState = previous,
                    NextState = next,
                    ReleasePayload = next == 공간LandscapeGraphStreamingState.Unloaded,
                });
                states[missing] = next;
                if (next == 공간LandscapeGraphStreamingState.Unloaded) hashes.Remove(missing);
            }

            var cached = states.Where(value => value.Value == 공간LandscapeGraphStreamingState.Cached)
                .Select(value => value.Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (var excess in cached.Take(Math.Max(0, cached.Length - maximumCachedGraphs)))
            {
                states[excess] = 공간LandscapeGraphStreamingState.Unloaded;
                hashes.Remove(excess);
                var decision = decisions.Last(value => value.LandscapeGraphStableId == excess);
                decision.NextState = 공간LandscapeGraphStreamingState.Unloaded;
                decision.ReleasePayload = true;
            }
            return decisions;
        }

        private static 공간LandscapeGraphStreamingState DetermineNextState(
            공간LandscapeGraphDescriptorData descriptor,
            공간LandscapeGraphStreamingState previous,
            ISet<string> activeTileKeys,
            ISet<string> preparedTileKeys)
        {
            if (!descriptor.CanLoad) return 공간LandscapeGraphStreamingState.Declared;
            if (descriptor.Intersects(activeTileKeys))
                return 공간LandscapeGraphStreamingState.Active;
            if (descriptor.Intersects(preparedTileKeys))
                return 공간LandscapeGraphStreamingState.Prepared;
            return previous is 공간LandscapeGraphStreamingState.Active
                    or 공간LandscapeGraphStreamingState.Prepared
                    or 공간LandscapeGraphStreamingState.Cached
                ? 공간LandscapeGraphStreamingState.Cached
                : 공간LandscapeGraphStreamingState.Declared;
        }

        private static bool NeedsPayloadLoad(
            공간LandscapeGraphDescriptorData descriptor,
            공간LandscapeGraphStreamingState previous,
            공간LandscapeGraphStreamingState next,
            string previousHash) =>
            next is 공간LandscapeGraphStreamingState.Active
                or 공간LandscapeGraphStreamingState.Prepared
            && (previous is 공간LandscapeGraphStreamingState.Unloaded
                    or 공간LandscapeGraphStreamingState.Declared
                || previousHash != descriptor.GraphHashSha256);

        private static 공간LandscapeGraphStreamingState MissingGraphNextState(
            공간LandscapeGraphStreamingState previous) =>
            previous == 공간LandscapeGraphStreamingState.Cached
                ? 공간LandscapeGraphStreamingState.Unloaded
                : 공간LandscapeGraphStreamingState.Cached;
    }

    /// <summary>
    /// 서버의 AreaSet·Graph 빌드 상태와 플레이어별 스트리밍 상태를 결합한다.
    /// 서버 payload를 수정하지 않으며, 동일 hash의 Cached Graph는 다시 받지 않는다.
    /// </summary>
    public sealed class 공간LandscapeGraphStreamingSession
    {
        private readonly I공간AreaSetLandscapeGraphRepository repository;
        private readonly 공간LandscapeGraphStreamingPlanner planner;
        private readonly Dictionary<string, 공간LandscapeGraphData> payloads =
            new Dictionary<string, 공간LandscapeGraphData>(StringComparer.Ordinal);

        public 공간LandscapeGraphStreamingSession(
            I공간AreaSetLandscapeGraphRepository sourceRepository,
            공간LandscapeGraphStreamingPlanner sourcePlanner = null)
        {
            repository = sourceRepository
                         ?? throw new ArgumentNullException(nameof(sourceRepository));
            planner = sourcePlanner ?? new 공간LandscapeGraphStreamingPlanner();
        }

        public 공간AreaSetDefinitionData AreaSet { get; private set; }

        public async Task<공간AreaSetDefinitionData> InitializeAsync(
            string areaSetStableId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(areaSetStableId))
                throw new InvalidOperationException("WorldAreaSetStableIdMissing");
            var value = await repository.LoadAreaSetAsync(areaSetStableId, cancellationToken);
            value.Validate();
            AreaSet = value;
            return value;
        }

        public async Task<공간LandscapeGraphStreamingBatch> RefreshAsync(
            string centerTileKey,
            ISet<string> activeTileKeys,
            ISet<string> preparedTileKeys,
            int radiusTiles,
            CancellationToken cancellationToken)
        {
            if (AreaSet == null)
                throw new InvalidOperationException("WorldAreaSetStreamingNotInitialized");
            var index = await repository.LoadGraphIndexAsync(
                AreaSet.AreaSetStableId, centerTileKey, radiusTiles, cancellationToken);
            index.Validate();
            if (!string.Equals(index.AreaSetStableId, AreaSet.AreaSetStableId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("WorldAreaSetGraphIndexMismatch");

            var decisions = planner.Reconcile(
                index, activeTileKeys, preparedTileKeys).ToArray();
            var descriptors = index.Graphs.ToDictionary(
                value => value.LandscapeGraphStableId, StringComparer.Ordinal);
            var loaded = new List<공간LandscapeGraphData>();
            var released = new List<string>();
            foreach (var decision in decisions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (decision.ReleasePayload)
                {
                    payloads.Remove(decision.LandscapeGraphStableId);
                    released.Add(decision.LandscapeGraphStableId);
                    continue;
                }
                if (!decision.NeedsPayloadLoad) continue;
                if (!descriptors.TryGetValue(decision.LandscapeGraphStableId, out var descriptor))
                    throw new InvalidOperationException("WorldLandscapeGraphDescriptorMissing");
                var graph = await LoadValidatedGraphAsync(descriptor, cancellationToken);
                payloads[graph.LandscapeGraphStableId] = graph;
                loaded.Add(graph);
            }

            EnsureRequiredPayloads(decisions);

            return new 공간LandscapeGraphStreamingBatch
            {
                AreaSet = AreaSet,
                Index = index,
                Decisions = decisions,
                LoadedGraphs = loaded.ToArray(),
                ReleasedGraphStableIds = released.ToArray(),
            };
        }

        public bool TryGetGraph(string landscapeGraphStableId, out 공간LandscapeGraphData graph)
            => payloads.TryGetValue(landscapeGraphStableId, out graph);

        private async Task<공간LandscapeGraphData> LoadValidatedGraphAsync(
            공간LandscapeGraphDescriptorData descriptor,
            CancellationToken cancellationToken)
        {
            var graph = await repository.LoadGraphAsync(
                descriptor.LandscapeGraphStableId, cancellationToken);
            graph.Validate();
            if (!string.Equals(graph.AreaSetStableId, AreaSet.AreaSetStableId,
                    StringComparison.Ordinal)
                || !string.Equals(graph.LandscapeGraphStableId,
                    descriptor.LandscapeGraphStableId, StringComparison.Ordinal)
                || !string.Equals(graph.GraphHashSha256,
                    descriptor.GraphHashSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WorldLandscapeGraphPayloadMismatch");
            return graph;
        }

        private void EnsureRequiredPayloads(
            IEnumerable<공간LandscapeGraphStreamingDecision> decisions)
        {
            foreach (var decision in decisions.Where(value =>
                         value.NextState is 공간LandscapeGraphStreamingState.Active
                             or 공간LandscapeGraphStreamingState.Prepared))
                if (!payloads.ContainsKey(decision.LandscapeGraphStableId))
                    throw new InvalidOperationException(
                        "WorldLandscapeGraphPayloadMissing:" + decision.LandscapeGraphStableId);
        }
    }
}
