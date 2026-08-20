using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 하나의 LandscapeGraph가 참조하는 여러 L2 타일 조각을 하나의 교체 단위로 조립한다.
    /// Tile은 좌표·캐시 단위로 유지하고, 화면에는 Graph root만 원자적으로 교체한다.
    /// </summary>
    public sealed class 공간AreaSetLandscapeGraphRuntimeAssembler
    {
        private readonly 공간문법LandscapeRuntimeAssembler tileAssembler;
        private readonly float tileWorldSize;

        public 공간AreaSetLandscapeGraphRuntimeAssembler(
            공간문법CompositionCatalog catalog,
            float compressedTileWorldSize)
        {
            if (compressedTileWorldSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(compressedTileWorldSize));
            tileWorldSize = compressedTileWorldSize;
            tileAssembler = new 공간문법LandscapeRuntimeAssembler(
                catalog, compressedTileWorldSize);
        }

        public 공간AreaSetLandscapeGraphRuntimeAssembler(
            공간문법CompositionCatalog catalog,
            공간문법SyntyBindingCatalog bindingCatalog,
            float compressedTileWorldSize)
        {
            if (compressedTileWorldSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(compressedTileWorldSize));
            tileWorldSize = compressedTileWorldSize;
            tileAssembler = new 공간문법LandscapeRuntimeAssembler(
                catalog, bindingCatalog, compressedTileWorldSize);
        }

        public GameObject BuildStaging(
            공간LandscapeGraphData graph,
            Transform areaSetRoot,
            string anchorTileKey)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (areaSetRoot == null) throw new ArgumentNullException(nameof(areaSetRoot));
            graph.Validate();
            if (!graph.CanAssemble)
                throw new InvalidOperationException("WorldLandscapeGraphNotReady");
            var scenarioLocal = anchorTileKey.StartsWith(
                "scenario-local:", StringComparison.Ordinal);
            if (!scenarioLocal
                && !공간TileWindowPlanner.TryParse(anchorTileKey, out _, out _))
                throw new InvalidOperationException("WorldLandscapeGraphAnchorTileInvalid");

            var graphRoot = new GameObject("LandscapeGraphRoot_Staging");
            graphRoot.SetActive(false);
            graphRoot.transform.SetParent(areaSetRoot, false);
            try
            {
                foreach (var tileKey in graph.TileRefs.OrderBy(value => value, StringComparer.Ordinal))
                {
                    var tileData = graph.ToTileData(tileKey);
                    if (!tileData.CanAssemble || tileData.Placements.Length == 0) continue;
                    if (!scenarioLocal
                        && !공간TileWindowPlanner.TryParse(tileKey, out _, out _))
                        throw new InvalidOperationException("WorldLandscapeGraphTileKeyInvalid");

                    var fragment = new GameObject("TileFragment_" + SafeName(tileKey));
                    fragment.transform.SetParent(graphRoot.transform, false);
                    if (scenarioLocal)
                    {
                        fragment.transform.localPosition = Vector3.zero;
                    }
                    else
                    {
                        공간TileWindowPlanner.TryParse(anchorTileKey,
                            out var anchorX, out var anchorY);
                        공간TileWindowPlanner.TryParse(tileKey,
                            out var tileX, out var tileY);
                        fragment.transform.localPosition = new Vector3(
                            (tileX - anchorX) * tileWorldSize,
                            0f,
                            (tileY - anchorY) * tileWorldSize);
                    }
                    var staging = tileAssembler.BuildStaging(tileData, fragment.transform);
                    staging.name = "LandscapeCompositionRoot";
                    staging.SetActive(true);
                }
                return graphRoot;
            }
            catch
            {
                Destroy(graphRoot);
                throw;
            }
        }

        public static void ApplyBatch(
            공간LandscapeGraphStreamingBatch batch,
            IDictionary<string, GameObject> graphRoots,
            Func<공간LandscapeGraphData, GameObject> buildStaging)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (graphRoots == null) throw new ArgumentNullException(nameof(graphRoots));
            if (buildStaging == null) throw new ArgumentNullException(nameof(buildStaging));

            foreach (var graph in batch.LoadedGraphs.OrderBy(
                         value => value.LandscapeGraphStableId, StringComparer.Ordinal))
            {
                var staging = buildStaging(graph);
                graphRoots.TryGetValue(graph.LandscapeGraphStableId, out var current);
                var decision = batch.Decisions.Single(value =>
                    value.LandscapeGraphStableId == graph.LandscapeGraphStableId);
                CommitAtomic(ref current, staging,
                    decision.NextState == 공간LandscapeGraphStreamingState.Active);
                graphRoots[graph.LandscapeGraphStableId] = current;
            }

            foreach (var decision in batch.Decisions)
            {
                if (!graphRoots.TryGetValue(decision.LandscapeGraphStableId, out var root))
                    continue;
                if (decision.ReleasePayload)
                {
                    graphRoots.Remove(decision.LandscapeGraphStableId);
                    Destroy(root);
                    continue;
                }
                root.SetActive(decision.NextState == 공간LandscapeGraphStreamingState.Active);
            }
        }

        public static void CommitAtomic(
            ref GameObject currentRoot,
            GameObject stagingRoot,
            bool activate)
        {
            if (stagingRoot == null)
                throw new ArgumentNullException(nameof(stagingRoot));
            var previous = currentRoot;
            stagingRoot.name = "LandscapeGraphRoot";
            stagingRoot.SetActive(activate);
            currentRoot = stagingRoot;
            if (previous == null) return;
            previous.name = "LandscapeGraphRoot_Retired";
            previous.SetActive(false);
            Destroy(previous);
        }

        private static string SafeName(string value) => value.Replace(':', '_').Replace('/', '_');

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
