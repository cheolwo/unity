using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public interface I공간문법MicroDetailGenerator
    {
        void GenerateMicroDetail(int deterministicSeed, string generatorRevision);
    }

    [DisallowMultipleComponent]
    public sealed class 공간문법PlacementInstanceView : MonoBehaviour
    {
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string compositionKey = string.Empty;
        [SerializeField] private string evidenceKindCode = string.Empty;
        [SerializeField] private string graphHashSha256 = string.Empty;
        [SerializeField] private string detailGeneratorRevision = string.Empty;
        [SerializeField] private int deterministicSeed;
        [SerializeField] private double physicalElevationMeters;
        [SerializeField] private bool presentationOnly = true;

        public string PlacementStableId => placementStableId;
        public string CompositionKey => compositionKey;
        public int DeterministicSeed => deterministicSeed;
        public string DetailGeneratorRevision => detailGeneratorRevision;

        public void Configure(
            공간LandscapePlacementData placement,
            string graphHash,
            string generatorRevision)
        {
            placementStableId = placement.PlacementStableId;
            compositionKey = placement.CompositionKey;
            evidenceKindCode = placement.EvidenceKindCode;
            graphHashSha256 = graphHash;
            detailGeneratorRevision = generatorRevision;
            deterministicSeed = placement.DeterministicSeed;
            physicalElevationMeters = placement.PhysicalElevationMeters;
            presentationOnly = true;
        }
    }

    /// <summary>
    /// 서버의 Macro·Meso 경관 Graph를 의미 기반 Composition으로 해석한다.
    /// staging root 전체가 검증된 뒤 기존 root와 교환하므로 타일 갱신 중 반쪽 경관을 노출하지 않는다.
    /// </summary>
    public sealed class 공간문법LandscapeRuntimeAssembler
    {
        private readonly 공간문법CompositionCatalog catalog;
        private readonly float tileWorldSize;

        public 공간문법LandscapeRuntimeAssembler(
            공간문법CompositionCatalog sourceCatalog,
            float compressedTileWorldSize)
        {
            catalog = sourceCatalog != null
                ? sourceCatalog
                : throw new ArgumentNullException(nameof(sourceCatalog));
            if (compressedTileWorldSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(compressedTileWorldSize));
            tileWorldSize = compressedTileWorldSize;
            catalog.Validate();
        }

        public GameObject BuildStaging(
            공간LandscapeCompositionTileData data,
            Transform tileRoot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (tileRoot == null) throw new ArgumentNullException(nameof(tileRoot));
            data.Validate();
            catalog.Validate();
            if (!data.CanAssemble)
                throw new InvalidOperationException("WorldLandscapeCompositionNotReady");
            if (data.GrammarRevision != catalog.CatalogRevision
                || !string.Equals(data.GrammarHashSha256,
                    catalog.BuildSafeCatalogHashSha256(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WorldLandscapeGrammarCatalogMismatch");

            ValidateGraphReferences(data);
            var staging = new GameObject("LandscapeCompositionRoot_Staging");
            staging.SetActive(false);
            staging.transform.SetParent(tileRoot, false);
            try
            {
                var topologyRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var topology in 공간문법CompositionTopologyCodes.All)
                {
                    var root = new GameObject(TopologyRootName(topology));
                    root.transform.SetParent(staging.transform, false);
                    topologyRoots.Add(topology, root.transform);
                }

                foreach (var placement in data.Placements.OrderBy(
                             value => value.PlacementStableId, StringComparer.Ordinal))
                {
                    var entry = catalog.Resolve(placement.CompositionKey);
                    if (entry.TopologyCode != placement.TopologyCode)
                        throw new InvalidOperationException(
                            "WorldLandscapePlacementTopologyMismatch:" + placement.PlacementStableId);
                    var instance = UnityEngine.Object.Instantiate(
                        entry.Prefab, topologyRoots[entry.TopologyCode], false);
                    instance.name = "VisualRoot_" + SafeName(placement.PlacementStableId);
                    ApplyTransform(instance.transform, data.TileKey, placement);
                    var view = instance.GetComponent<공간문법PlacementInstanceView>()
                               ?? instance.AddComponent<공간문법PlacementInstanceView>();
                    view.Configure(placement, data.GraphHashSha256,
                        entry.InternalGeneration.DetailGeneratorRevision);
                    foreach (var generator in instance.GetComponentsInChildren<MonoBehaviour>(true)
                                 .OfType<I공간문법MicroDetailGenerator>())
                        generator.GenerateMicroDetail(
                            placement.DeterministicSeed,
                            entry.InternalGeneration.DetailGeneratorRevision);
                }
                return staging;
            }
            catch
            {
                Destroy(staging);
                throw;
            }
        }

        public static void CommitAtomic(ref GameObject currentRoot, GameObject stagingRoot)
        {
            if (stagingRoot == null)
                throw new ArgumentNullException(nameof(stagingRoot));
            var previous = currentRoot;
            stagingRoot.name = "LandscapeCompositionRoot";
            stagingRoot.SetActive(true);
            currentRoot = stagingRoot;
            if (previous == null) return;
            previous.name = "LandscapeCompositionRoot_Retired";
            previous.SetActive(false);
            Destroy(previous);
        }

        private void ApplyTransform(
            Transform target,
            string tileKey,
            공간LandscapePlacementData placement)
        {
            if (!공간TileWindowPlanner.TryParse(tileKey, out var tileX, out var tileY))
                throw new InvalidOperationException("WorldLandscapeTileKeyInvalid");
            const double tileSizeMeters = 500d;
            var centerEasting = tileX * tileSizeMeters + tileSizeMeters * .5d;
            var centerNorthing = tileY * tileSizeMeters + tileSizeMeters * .5d;
            var metersToWorld = tileWorldSize / (float)tileSizeMeters;
            target.localPosition = new Vector3(
                (float)(placement.EastingMeters - centerEasting) * metersToWorld,
                0f,
                (float)(placement.NorthingMeters - centerNorthing) * metersToWorld);
            target.localRotation = Quaternion.Euler(0f, (float)placement.RotationDegrees, 0f);
            target.localScale = new Vector3(
                placement.Mirrored ? -metersToWorld : metersToWorld,
                metersToWorld,
                metersToWorld);
        }

        private static void ValidateGraphReferences(공간LandscapeCompositionTileData data)
        {
            var nodes = new HashSet<string>(
                data.Nodes.Select(value => value.NodeStableId), StringComparer.Ordinal);
            var placements = new HashSet<string>(
                data.Placements.Select(value => value.PlacementStableId), StringComparer.Ordinal);
            foreach (var edge in data.Edges)
                if (!nodes.Contains(edge.FromNodeStableId) || !nodes.Contains(edge.ToNodeStableId))
                    throw new InvalidOperationException(
                        "WorldLandscapeEdgeNodeMissing:" + edge.EdgeStableId);
            foreach (var placement in data.Placements)
                if (!nodes.Contains(placement.NodeStableId))
                    throw new InvalidOperationException(
                        "WorldLandscapePlacementNodeMissing:" + placement.PlacementStableId);
            foreach (var stub in data.ExternalConnectorStubs)
                if (!placements.Contains(stub.PlacementStableId))
                    throw new InvalidOperationException(
                        "WorldLandscapeExternalConnectorPlacementMissing:" + stub.StubStableId);
        }

        private static string TopologyRootName(string topology) => topology switch
        {
            공간문법CompositionTopologyCodes.Area => "면형모판",
            공간문법CompositionTopologyCodes.Linear => "선형모판",
            공간문법CompositionTopologyCodes.Junction => "결절모판",
            공간문법CompositionTopologyCodes.Transition => "경계봉합모판",
            공간문법CompositionTopologyCodes.Landmark => "거점모판",
            공간문법CompositionTopologyCodes.Detail => "세부모판",
            _ => throw new InvalidOperationException("LandscapeGrammarTopologyUnknown:" + topology),
        };

        private static string SafeName(string value) => value.Replace(':', '_').Replace('/', '_');

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
